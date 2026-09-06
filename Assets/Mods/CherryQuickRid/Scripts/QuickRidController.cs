#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.PlacementSystem;
using Buildings.Outdoors;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using Streets;
using UI;
using UI.Guiders;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace CherryQuickRid
{
    /// <summary>
    /// Zentrale Laufzeitlogik: Online/Offline, Auftragsgenerierung, Fahrgast, Ankunft, Abrechnung.
    /// Stufenplan siehe Docs~/IDEEN.md. Vorbild: der Vanilla-Lieferjob
    /// (DeliveryJobStartController, DeliveryJobVehicle in _reference/GameSource~).
    /// </summary>
    public sealed class QuickRidController : MonoBehaviour
    {
        /// <summary>
        /// So weit liegt <c>endTime</c> der Schicht in der Zukunft. Ein Fahrdienst hat keine Schicht;
        /// <c>PlayerMission.IsOngoing()</c> ist aber nicht virtual, also bleibt nur eine Frist, die nie
        /// abläuft. Kein Spielcode wertet die Frist einer fremden Missionsklasse aus (Docs~/IDEEN.md).
        /// </summary>
        private const int OpenEndDays = 3650;

        /// <summary>
        /// Transaktionstyp der Auszahlung: bewusst der Vanilla-Typ des Angestelltengehalts, nicht ein
        /// eigener Key.
        /// </summary>
        /// <remarks>
        /// Die Onkel-Aufgabe „Verdiene $200 mit deinem Job" prüft über <c>Tutorial.HasEarnedMoney</c>
        /// die gespeicherten Transaktionen und filtert dabei auf drei Typen:
        /// <c>ba:transaction_playerjobsalary</c>, <c>ba:transaction_deliveryjobwage</c> und
        /// <c>ba:transaction_fooddeliveryjobwage</c> (Asset <c>QuestFindAJobRequirementEarn200</c> im
        /// Bundle <c>questfindajob</c>). Ein eigener Typ zählt dort nie mit, und die Bedingung ist
        /// nicht erweiterbar – deshalb bucht QuickRid unter einem der drei.
        /// <para>
        /// Von den dreien nimmt nur dieser einen Platzhalter auf: „Gehalt von {businessName}". Mit
        /// <see cref="TransactionBusinessName"/> steht QuickRid damit namentlich in der Buchung,
        /// statt sie als Lieferjob auszugeben. Die Typ-Spalte in EconoView zeigt „Gehalt".
        /// </para>
        /// </remarks>
        private const string TransactionKey = "ba:transaction_playerjobsalary";
        private const string TransactionCategory = "ba:transactioncategory_salaryincome";

        /// <summary>
        /// Wert für <c>{businessName}</c> in der Buchung. Bewusst ein Literal und kein Locale-Key:
        /// der Wert landet im Spielstand und würde sonst die Sprache des Buchungszeitpunkts einfrieren.
        /// </summary>
        private const string TransactionBusinessName = "QuickRid";

        private const string ButtonName = "QuickRid - Job Button";

        /// <summary>
        /// Fahrpreisformel, eingeordnet zwischen die beiden Vanilla-Einstiegsjobs.
        /// </summary>
        /// <remarks>
        /// Die Essenslieferung zahlt <c>30 + Distanz × 0,08</c> und reicht höchstens 600 m weit
        /// (FoodDeliveryJobConfig: baseReward, rewardPerMeter, destinationRadius) – das sind 54 $ auf
        /// 300 m und 78 $ auf 600 m. Der Lieferfahrer bringt 100 $ je Ziel bei drei Zielen, also rund
        /// 300 $ brutto abzüglich Reparaturkosten (DeliveryJobStartLocation.deliveryReward,
        /// DeliveryJobHelper.TryDeliverToDestination). QuickRid liegt mit gleicher Pauschale und
        /// leicht höherem Meterpreis dazwischen: 60 $ auf 300 m, 90 $ auf 600 m, 180 $ auf 1500 m.
        /// <para>
        /// <see cref="MinimumFare"/> greift bei diesen Werten nicht mehr und bleibt nur als Absicherung
        /// stehen, falls Multiplikator und Rating-Modifier gemeinsam nach unten laufen.
        /// </para>
        /// </remarks>
        private const float FixedBaseFare = 30f;
        private const float FarePerMeter = 0.10f;
        private const float MinimumFare = 10f;

        /// <summary>Schrittgeschwindigkeit in m/s – bis hierher gilt das Auto als "steht".</summary>
        private const float WalkingSpeed = 1.5f;

        /// <summary>So viele Türen werden pro Anfrage höchstens auf dem NavMesh probiert.</summary>
        private const int PickupSampleAttempts = 10;

        /// <summary>
        /// So viele Gebäude prüft der Cache-Aufbau pro Frame. Jede Prüfung scannt alle Waypoints
        /// der Stadt, deshalb wird der Aufbau über mehrere Frames verteilt.
        /// </summary>
        private const int AddressCacheBuildingsPerFrame = 8;

        /// <summary>
        /// Ab diesem Anteil verworfener Türen warnt das Log: dann ist vermutlich
        /// <see cref="QuickRidSettings.RoadProximityMeters"/> zu streng und nicht die Stadt schuld.
        /// </summary>
        private const float AddressCacheRejectWarnRatio = 0.5f;

        private const int PickupCircleSegments = 72;

        private ModContext? _context;
        private QuickRidTasksUI? _tasksUi;

        /// <summary>Kartenfilter in der Kategorie Jobs samt Kartenpunkt der laufenden Fahrt.</summary>
        private QuickRidMapFilter? _mapFilter;

        private Button? _jobButton;
        private TextLocalizationComponent? _jobButtonLabel;
        private CarController? _currentCar;

        /// <summary>Originalzustand des geklonten Buttons, Basis fuer beide Farbzustaende.</summary>
        private ColorBlock _defaultColors;
        private Color _defaultGraphicColor;
        private bool _colorsCaptured;

        /// <summary>Das Auto der laufenden Schicht – auch dann, wenn der Spieler gerade nicht darin sitzt.</summary>
        private CarController? _missionCar;
        private Rigidbody? _missionCarBody;

        /// <summary>Mehrere Fahrgäste mit je eigenem Aussehen; pro Fahrt wird einer gezogen.</summary>
        private readonly QuickRidPassengerPool _passengers = new QuickRidPassengerPool();

        /// <summary>
        /// Ring an der Abholung. Eigener Root statt eines Kindes des Fahrgasts: welcher Fahrgast
        /// aus dem Pool gerade dasteht, wechselt von Fahrt zu Fahrt.
        /// </summary>
        private GameObject? _pickupRoot;
        private LineRenderer? _pickupCircle;

        /// <summary>Ring am Ziel, sichtbar solange ein Fahrgast an Bord ist.</summary>
        private GameObject? _dropoffRoot;
        private LineRenderer? _dropoffCircle;

        /// <summary>Gemeinsames Material beider Ringe. Wird erst in <see cref="OnDestroy"/> freigegeben.</summary>
        private Material? _circleMaterial;

        /// <summary>Merkt einen fehlgeschlagenen Ring-Aufbau, damit der Tick es nicht endlos wiederholt.</summary>
        private bool _circleCreationFailed;

        /// <summary>Weltposition des wartenden Fahrgasts. Nur Laufzeit – siehe QuickRidMission.pickupAddress.</summary>
        private Vector3 _pickupPosition;
        private Quaternion _pickupRotation = Quaternion.identity;

        /// <summary>Verhindert, dass der Angebotsdialog jeden Frame erneut geöffnet wird.</summary>
        private bool _offerDialogOpen;

        /// <summary>Alle anfahrbaren Gebäudetüren, einmal pro Stadtladen aufgebaut.</summary>
        private List<AddressCandidate>? _addressCandidates;

        /// <summary>Erst wenn der Aufbau durch ist, dürfen Anfragen entstehen.</summary>
        private bool _addressCacheReady;

        private Coroutine? _addressCacheRoutine;

        private readonly List<AddressCandidate> _nearbyBuffer = new List<AddressCandidate>();
        private readonly List<AddressCandidate> _destinationBuffer = new List<AddressCandidate>();

        /// <summary>Eine Gebäudetür als möglicher Abhol- oder Zielpunkt.</summary>
        private struct AddressCandidate
        {
            public Address address;
            public Transform door;
        }

        /// <summary>Die laufende Schicht, oder null wenn der Spieler offline ist.</summary>
        private static QuickRidMission? Mission =>
            SaveGameManager.Current?.currentPlayerMission as QuickRidMission;

        /// <summary>Log der Mod, auch für <see cref="QuickRidTasksUI"/>.</summary>
        public IModLogger? Logger => _context?.Logger;

        /// <param name="mapIcon">
        /// Symbol des Kartenfilters aus dem AssetBundle; null lässt den Filter auf das Symbol des
        /// Vanilla-Lieferjobs zurückfallen.
        /// </param>
        public void Initialize(ModContext context, Sprite? mapIcon)
        {
            _context = context;
            _mapFilter = new QuickRidMapFilter(context.Logger, mapIcon);
        }

        private void Start()
        {
            _tasksUi = new QuickRidTasksUI(this);
            GlobalEvents.onEnterVehicle += OnEnterVehicle;
            GlobalEvents.onExitVehicle += OnExitVehicle;
            GlobalEvents.onPause += OnPaused;
            GlobalEvents.onGameUnloaded += OnGameUnloaded;
            GlobalEvents.RegisterOnGameLoadedLateCallback(RestoreState);
        }

        private void OnDestroy()
        {
            GlobalEvents.onEnterVehicle -= OnEnterVehicle;
            GlobalEvents.onExitVehicle -= OnExitVehicle;
            GlobalEvents.onPause -= OnPaused;
            GlobalEvents.onGameUnloaded -= OnGameUnloaded;

            if (_jobButton != null)
                Destroy(_jobButton.gameObject);

            _jobButton = null;
            _jobButtonLabel = null;
            _currentCar = null;

            DestroyVisuals();
            GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);

            _mapFilter?.Destroy();

            _tasksUi?.Dispose();
            _tasksUi = null;
        }

        /// <summary>
        /// Beim Verlassen der Stadt bleibt dieser Controller (DontDestroyOnLoad) am Leben, alle
        /// Szenenobjekte darunter aber nicht. <c>SaveGameManager.Current</c> wird dabei nicht genullt,
        /// die Mission gilt also weiter als "läuft" – ohne dieses Aufräumen würde der nächste Tick
        /// auf zerstörte Objekte zugreifen.
        /// </summary>
        private void OnGameUnloaded()
        {
            _tasksUi?.Dispose();

            StopAddressCacheBuild();
            _addressCandidates = null;
            _nearbyBuffer.Clear();
            _destinationBuffer.Clear();

            _missionCar = null;
            _missionCarBody = null;
            _currentCar = null;
            _offerDialogOpen = false;

            _jobButton = null;
            _jobButtonLabel = null;
            _colorsCaptured = false;

            // Fahrgäste, Ringe, Kartenfilter und Material hängen an der Stadt-Szene und sterben
            // mit ihr.
            _passengers.ForgetSceneObjects();
            _mapFilter?.ForgetSceneObjects();
            _pickupRoot = null;
            _pickupCircle = null;
            _dropoffRoot = null;
            _dropoffCircle = null;
            _circleMaterial = null;
            _circleCreationFailed = false;
        }

        private void Update()
        {
            if (SaveGameManager.Current == null || LoadScene.isLoading || GameManager.isCitySceneBeingUnloaded)
                return;

            if (!InstanceBehavior<CityManager>.IsInitialized)
                return;

            // Der Kartenfilter steht auch offline in der Liste – deshalb vor dem Missions-Guard.
            _mapFilter?.Tick();

            QuickRidMission? mission = Mission;
            if (mission == null)
                return;

            RebindMissionCar(mission);

            switch (mission.state)
            {
                case QuickRidTripState.Waiting:
                    UpdateWaiting(mission);
                    break;
                case QuickRidTripState.Offered:
                    UpdateOffered(mission);
                    break;
                case QuickRidTripState.PassengerWaiting:
                    UpdatePassengerWaiting(mission);
                    break;
                case QuickRidTripState.PassengerAboard:
                    UpdatePassengerAboard(mission);
                    break;
            }
        }

        /// <summary>
        /// Nach dem Laden eines Spielstands: Aufgabenpanel wiederherstellen und den Button nachziehen,
        /// falls der Spieler bereits im Auto sitzt (dann ist onEnterVehicle schon durch).
        /// Eine laufende Fahrt wird abgebrochen – ihre Wiederherstellung ist Stufe 5 (Docs~/IDEEN.md).
        /// Der Abbruch kostet keinen Stern: er ist eine technische Einschränkung, keine Entscheidung
        /// des Spielers.
        /// </summary>
        private void RestoreState()
        {
            // "Wie Spiel" lässt sich erst hier auflösen: beim Registrieren der Options gibt es noch
            // keinen Spielstand, und die Schwierigkeit kann sich im laufenden Spiel noch ändern.
            if (QuickRidSettings.DifficultyChoice == QuickRidDifficultyChoice.MatchGame)
                QuickRidDifficulty.Apply(_context?.Logger);

            StartAddressCacheBuild();

            QuickRidMission? mission = Mission;

            if (mission != null)
            {
                // Statistik kommt aus modData, nicht aus der gespeicherten Mission – so gilt überall
                // derselbe Stand, auch wenn der Spielstand mitten in einer Schicht geschrieben wurde.
                QuickRidStats.LoadInto(mission);

                // Spielstände aus Stufe 2/3 tragen noch die feste 24-Stunden-Frist.
                mission.endTime = CreateOpenEndTime();
                mission.timeLimitMinutes = 0;
            }

            if (mission != null)
                RestoreTrip(mission);

            if (_tasksUi != null && mission != null)
                _tasksUi.Init();

            VehicleController current = VehicleHelper.GetCurrentVehicleBase();
            if (current != null)
                OnEnterVehicle(current);
        }

        /// <summary>
        /// Setzt die beim Speichern laufende Fahrt fort. Ein offenes Angebot wird verworfen – es war
        /// ohnehin befristet und der Dialog stünde nach dem Laden ohne Zusammenhang da.
        /// </summary>
        /// <remarks>
        /// Der wartende Fahrgast wird nicht an seiner alten Position wiederhergestellt, sondern neu an
        /// der Tür seiner Abholadresse abgesetzt (die Position steht nicht im Spielstand, siehe
        /// <see cref="QuickRidMission.pickupAddress"/>). Findet sich dort kein Platz auf dem NavMesh,
        /// bricht die Fahrt ab wie vor Stufe 5.
        /// </remarks>
        private void RestoreTrip(QuickRidMission mission)
        {
            switch (mission.state)
            {
                case QuickRidTripState.Waiting:
                    return;

                case QuickRidTripState.Offered:
                    mission.ClearTrip();
                    mission.nextRequestTime = CreateWaitDeadline();
                    GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
                    _mapFilter?.Clear();
                    _context?.Logger.Info("QuickRid: offenes Angebot beim Laden verworfen.");
                    return;

                case QuickRidTripState.PassengerWaiting:
                    if (mission.pickupAddress == null)
                        break;

                    Transform door = BuildingHelper.GetAddressEntranceTransform(mission.pickupAddress);
                    if (door == null || !TryResolvePickupSpot(door, out Vector3 position, out Quaternion rotation))
                        break;

                    _pickupPosition = position;
                    _pickupRotation = rotation;

                    ShowPassenger(_pickupPosition, _pickupRotation);
                    PinPickup(mission);
                    _mapFilter?.ShowPickup(_pickupPosition, mission.pickupAddress);
                    _context?.Logger.Info("QuickRid: wartender Fahrgast beim Laden wiederhergestellt.");
                    return;

                case QuickRidTripState.PassengerAboard:
                    if (mission.destinationAddress == null)
                        break;

                    // Fahrgast sitzt im Auto: nur Kartenpin und Zielring fehlen. boardingTime und
                    // damageAtBoarding stammen aus dem Spielstand und gelten unverändert weiter.
                    GuidersManager.SetGuiderTarget(mission.destinationAddress, DirectionGuiderType.JobDestination);
                    EnsureDropoffCircle(mission);
                    _mapFilter?.ShowDestination(mission.destinationAddress);
                    _context?.Logger.Info("QuickRid: laufende Fahrt beim Laden fortgesetzt.");
                    return;
            }

            AbortTrip(mission);
            _context?.Logger.Warn("QuickRid: Fahrt beim Laden nicht wiederherstellbar – abgebrochen.");
        }

        /// <summary>
        /// Sucht neben einer Gebäudetür einen Platz auf dem NavMesh, auf dem ein Fahrgast stehen kann.
        /// Die Blickrichtung zeigt vom Gebäude weg zur Straße – er schaut nach dem Auto aus.
        /// </summary>
        private static bool TryResolvePickupSpot(Transform door, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;

            Vector3 sampleAt = door.position + door.forward * 0.5f;
            if (!NavMesh.SamplePosition(sampleAt, out NavMeshHit hit, 2f, NavMeshHelper.NpcNavMeshFilter))
                return false;

            position = hit.position;

            Vector3 facing = hit.position - door.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                rotation = Quaternion.LookRotation(facing);

            return true;
        }

        // --- Fahrtanfrage und Fahrt ---------------------------------------------

        private void UpdateWaiting(QuickRidMission mission)
        {
            // Anfragen kommen nur, solange der Spieler tatsächlich im Missionsauto sitzt.
            if (!IsDrivingMissionCar(mission) || _missionCar == null)
                return;

            // Während der Cache baut, bleibt eine abgelaufene Frist stehen: sonst würde jeder Tick
            // sie neu setzen und die erste Anfrage käme viel später als gedacht.
            if (!_addressCacheReady)
                return;

            if (mission.nextRequestTime == null)
            {
                mission.nextRequestTime = CreateWaitDeadline();
                return;
            }

            if (!mission.nextRequestTime.IsInThePast())
                return;

            if (TryCreateRequest(mission, _missionCar.transform.position))
                mission.nextRequestTime = null;
            else
                mission.nextRequestTime = CreateWaitDeadline(); // nichts Passendes in Reichweite

            _tasksUi?.UpdateUI();
        }

        private void UpdateOffered(QuickRidMission mission)
        {
            // Verfall nur, solange der Dialog nicht offen ist – wer ihn vor sich hat, darf entscheiden.
            if (!_offerDialogOpen && mission.offerExpiryTime != null && mission.offerExpiryTime.IsInThePast())
            {
                mission.ClearTrip();
                mission.nextRequestTime = CreateWaitDeadline();
                _tasksUi?.UpdateUI();
                return;
            }

            if (_offerDialogOpen || HudConfirm.isOpen)
                return;

            if (!IsDrivingMissionCar(mission) || !IsMissionCarStopped())
                return;

            ShowOffer(mission);
        }

        private void UpdatePassengerWaiting(QuickRidMission mission)
        {
            if (!IsDrivingMissionCar(mission) || _missionCar == null || !IsMissionCarStopped())
                return;

            float radius = QuickRidSettings.PickupRadiusMeters;
            if ((_missionCar.transform.position - _pickupPosition).sqrMagnitude > radius * radius)
                return;

            HidePassenger();

            if (mission.destinationAddress != null)
            {
                GuidersManager.SetGuiderTarget(mission.destinationAddress, DirectionGuiderType.JobDestination);
                _mapFilter?.ShowDestination(mission.destinationAddress);
            }

            // Ab hier läuft die bewertete Fahrzeit; Schaden zählt nur als Zuwachs ab jetzt.
            mission.boardingTime = TimeHelper.Now();
            mission.damageAtBoarding = _missionCar.vehicleInstance != null ? _missionCar.vehicleInstance.damage : 0f;

            mission.state = QuickRidTripState.PassengerAboard;
            Notifications.Show(NotificationType.Info, "quickrid_passenger_aboard");
            _tasksUi?.UpdateUI();
        }

        private void UpdatePassengerAboard(QuickRidMission mission)
        {
            if (mission.destinationAddress == null)
            {
                AbortTrip(mission);
                return;
            }

            // Vor den Fahrzeugprüfungen: der Ring soll schon während der Fahrt zu sehen sein.
            EnsureDropoffCircle(mission);

            if (!IsDrivingMissionCar(mission) || _missionCar == null || !IsMissionCarStopped())
                return;

            Transform entrance = BuildingHelper.GetAddressEntranceTransform(mission.destinationAddress);
            if (entrance == null)
                return;

            float radius = QuickRidSettings.DropoffRadiusMeters;
            if ((_missionCar.transform.position - entrance.position).sqrMagnitude > radius * radius)
                return;

            CompleteTrip(mission, _missionCar);
        }

        /// <summary>
        /// Fahrgast abgesetzt: Sterne berechnen, Fahrpreis auszahlen, Statistik fortschreiben.
        /// Vorbild für die Auszahlung: DeliveryJobVehicle.GiveEarningsAndReset im Vanilla-Lieferjob.
        /// </summary>
        /// <remarks>
        /// Ausgezahlt wird <c>mission.fare</c> unverändert – der Rating-Modifier steckt seit der Anfrage
        /// darin, damit der Preis aus dem Dialog dem gezahlten entspricht. Zeit und Schaden kommen aus
        /// <see cref="GetTripProgress"/>, damit die Vorschau im Panel dieselbe Rechnung zeigt.
        /// </remarks>
        private void CompleteTrip(QuickRidMission mission, CarController car)
        {
            GetTripProgress(mission, car, out float elapsedMinutes, out float allowedMinutes, out float damageTaken);

            int stars = QuickRidRating.CalculateStars(elapsedMinutes, allowedMinutes, damageTaken);
            float fare = mission.fare;

            // Ein Wurf je Fahrt nach dem Muster von DeliveryJobTipsConfig.RollTip, zusätzlich an die
            // Sterne dieser Fahrt gekoppelt (unter 4 Sternen gibt es nie Trinkgeld).
            bool fast = QuickRidTips.IsFastTrip(elapsedMinutes, allowedMinutes);
            float share = QuickRidTips.Roll(fast, stars, out float tipRoll, out float tipChance);
            float tips = QuickRidTips.ToAmount(fare, share);

            var transactionData = new Dictionary<string, string> { { "businessName", TransactionBusinessName } };
            GameManager.ChangeMoneySafe(fare + tips, new TransactionInfo(TransactionKey, TransactionCategory, transactionData));

            QuickRidRating.Push(mission.GetRatingHistory(), stars);
            mission.completedTrips++;
            mission.totalEarnings += fare;
            mission.totalTips += tips;
            QuickRidStats.SaveFrom(mission);
            HideDropoffCircle();

            // Zähler dieser Sitzung für die Übersicht beim Offline-Gehen.
            mission.sessionTrips++;
            mission.sessionEarnings += fare;
            mission.sessionTips += tips;
            mission.sessionStarsTotal += stars;

            GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
            _mapFilter?.Clear();
            mission.ClearTrip();
            mission.nextRequestTime = CreateWaitDeadline();

            Notifications.Show(NotificationType.Success, "quickrid_trip_complete",
                new Dictionary<string, string>
                {
                    { "fare", fare.ToString("0") },
                    { "tips", tips.ToString("0") },
                    { "stars", stars.ToString() }
                });

            _context?.Logger.Info(
                $"QuickRid: Fahrt abgeschlossen – {elapsedMinutes:0}/{allowedMinutes:0} min" +
                $"{(fast ? " (schnell)" : string.Empty)}, Tarif {mission.tariffPeriod}, " +
                $"Schaden {damageTaken:0.000}, {stars} Sterne, ${fare:0} + ${tips:0} Trinkgeld, " +
                $"Schnitt {QuickRidRating.FormatAverage(mission.GetRatingHistory())}.");

            QuickRidLog.Dev(_context?.Logger,
                $"QuickRid: Trinkgeld-Wurf {tipRoll:0.000} gegen wirksame Chance {tipChance:0.000}, " +
                $"Anteil {share:0.00} des Fahrpreises.");

            _tasksUi?.UpdateUI();
        }

        private void AbortTrip(QuickRidMission mission)
        {
            HidePassenger();
            HideDropoffCircle();
            GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
            _mapFilter?.Clear();
            mission.ClearTrip();
            mission.nextRequestTime = CreateWaitDeadline();
            _tasksUi?.UpdateUI();
        }

        /// <summary>
        /// Stand der laufenden Fahrt: verstrichene Zeit, Zeitfenster und Schadenzuwachs seit dem
        /// Einstieg. Grundlage sowohl der Abrechnung als auch der Vorschau im Aufgabenpanel.
        /// </summary>
        /// <remarks>
        /// Die Dauer wird über <c>GetTotalMinutes</c> gerechnet;
        /// <c>Timestamp.GetDifferenceInMinutes</c> wird im Spielcode mit beiden Vorzeichenrichtungen
        /// benutzt und ist deshalb nicht verlässlich lesbar.
        /// </remarks>
        private static void GetTripProgress(QuickRidMission mission, CarController? car,
            out float elapsedMinutes, out float allowedMinutes, out float damageTaken)
        {
            allowedMinutes = QuickRidRating.AllowedMinutes(mission.tripDistance);
            elapsedMinutes = mission.boardingTime != null
                ? Mathf.Max(0f, TimeHelper.NowInMinutes() - mission.boardingTime.GetTotalMinutes())
                : 0f;

            // Ohne auffindbares Auto (z. B. in einem Gebäude geparkt) zählt nur die Zeit.
            float currentDamage = car != null && car.vehicleInstance != null
                ? car.vehicleInstance.damage
                : mission.damageAtBoarding;

            damageTaken = Mathf.Max(0f, currentDamage - mission.damageAtBoarding); // Reparatur unterwegs → 0
        }

        /// <summary>
        /// Für das Aufgabenpanel: Restzeit des Zeitfensters (negativ bei Überschreitung) und die
        /// Sterne, die ein sofortiges Absetzen ergäbe. Liefert false, wenn gerade keine Fahrt läuft.
        /// </summary>
        public bool TryGetTripPreview(QuickRidMission mission, out float remainingMinutes, out int stars)
        {
            remainingMinutes = 0f;
            stars = 5;

            if (mission.state != QuickRidTripState.PassengerAboard || mission.boardingTime == null)
                return false;

            GetTripProgress(mission, _missionCar, out float elapsed, out float allowed, out float damage);

            remainingMinutes = allowed - elapsed;
            stars = QuickRidRating.CalculateStars(elapsed, allowed, damage);
            return true;
        }

        /// <summary>
        /// Sucht einen Abholpunkt im Suchradius und ein Ziel in der erlaubten Fahrtstrecke.
        /// Bei Erfolg steht die Mission auf <see cref="QuickRidTripState.Offered"/>.
        /// </summary>
        private bool TryCreateRequest(QuickRidMission mission, Vector3 origin)
        {
            List<AddressCandidate>? candidates = _addressCandidates;
            if (!_addressCacheReady || candidates == null || candidates.Count == 0)
                return false;

            float searchRadius = QuickRidSettings.PassengerSearchRadiusMeters;
            float searchSqr = searchRadius * searchRadius;

            _nearbyBuffer.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                AddressCandidate candidate = candidates[i];
                if (candidate.door == null)
                    continue;

                // Sperrliste erst hier statt beim Cache-Aufbau: so wirken ein gemeldeter Ausschluss
                // und das Zurücksetzen sofort, ohne den Cache neu zu bauen.
                if (QuickRidBlacklist.Contains(candidate.address))
                    continue;

                if ((candidate.door.position - origin).sqrMagnitude <= searchSqr)
                    _nearbyBuffer.Add(candidate);
            }

            if (_nearbyBuffer.Count == 0)
                return false;

            // Erst zufällig ziehen, dann genau für diese Tür ein NavMesh-Sample – nicht für alle.
            // Die Teilmischung sorgt dafür, dass kein Kandidat zweimal probiert wird.
            Address? pickupAddress = null;
            Vector3 pickupPosition = default;
            Quaternion pickupRotation = Quaternion.identity;

            int attempts = Mathf.Min(PickupSampleAttempts, _nearbyBuffer.Count);
            for (int i = 0; i < attempts; i++)
            {
                int pick = UnityEngine.Random.Range(i, _nearbyBuffer.Count);
                AddressCandidate candidate = _nearbyBuffer[pick];
                _nearbyBuffer[pick] = _nearbyBuffer[i];
                _nearbyBuffer[i] = candidate;

                if (candidate.door == null)
                    continue;

                if (!TryResolvePickupSpot(candidate.door, out pickupPosition, out pickupRotation))
                    continue;

                pickupAddress = candidate.address;
                break;
            }

            if (pickupAddress == null)
                return false;

            float minDistance = Mathf.Min(QuickRidSettings.MinTripDistanceMeters, QuickRidSettings.MaxTripDistanceMeters);
            float maxDistance = Mathf.Max(QuickRidSettings.MinTripDistanceMeters, QuickRidSettings.MaxTripDistanceMeters);
            float minSqr = minDistance * minDistance;
            float maxSqr = maxDistance * maxDistance;

            _destinationBuffer.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                AddressCandidate candidate = candidates[i];
                if (candidate.door == null || candidate.address == pickupAddress)
                    continue;
                if (QuickRidBlacklist.Contains(candidate.address))
                    continue;

                float sqr = (candidate.door.position - pickupPosition).sqrMagnitude;
                if (sqr < minSqr || sqr > maxSqr)
                    continue;

                _destinationBuffer.Add(candidate);
            }

            if (_destinationBuffer.Count == 0)
                return false;

            AddressCandidate destination = _destinationBuffer[UnityEngine.Random.Range(0, _destinationBuffer.Count)];
            float distance = Vector3.Distance(destination.door.position, pickupPosition);

            _pickupPosition = pickupPosition;
            _pickupRotation = pickupRotation;

            mission.pickupAddress = pickupAddress;
            mission.destinationAddress = destination.address;
            mission.tripDistance = distance;
            mission.tariffPeriod = QuickRidTariff.CurrentPeriod();
            mission.fare = CalculateFare(distance, QuickRidRating.CurrentModifier(mission.GetRatingHistory()),
                QuickRidTariff.Multiplier(mission.tariffPeriod));
            mission.offerExpiryTime = CreateOfferDeadline();
            mission.state = QuickRidTripState.Offered;
            return true;
        }

        private void ShowOffer(QuickRidMission mission)
        {
            if (mission.pickupAddress == null || mission.destinationAddress == null)
            {
                AbortTrip(mission);
                return;
            }

            var body = new LanguageChangeEventDataHolder
            {
                Key = "quickrid_offer_body",
                Arguments = new
                {
                    pickup = AddressHelper.ToFormattedString(mission.pickupAddress),
                    destination = AddressHelper.ToFormattedString(mission.destinationAddress),
                    distance = UnitHelper.ToFormattedDistance(mission.tripDistance),
                    fare = mission.fare.ToString("0"),
                    // Der Aufschlag steht im Locale-Text der jeweiligen Tarifzeit.
                    tariff = QuickRidTariff.LocaleKey(mission.tariffPeriod).GetLocalization(),
                    // Das Zeitfenster läuft erst ab dem Einstieg – der Text sagt das dazu.
                    minutes = Mathf.RoundToInt(QuickRidRating.AllowedMinutes(mission.tripDistance))
                }
            };

            // allowConfirmationSkip: false – sonst nimmt eine gehaltene "ohne Bestätigung"-Taste
            // die Fahrt an, ohne dass der Spieler das Angebot je gesehen hat.
            _offerDialogOpen = true;
            HudConfirm.Show(
                new LanguageChangeEventDataHolder { Key = "quickrid_offer_title" },
                body,
                () => AcceptOffer(mission),
                () => DeclineOffer(mission),
                "quickrid_accept_ride",
                "quickrid_decline_ride",
                false);

            // Kam der Dialog nicht hoch (oder wurde er synchron beantwortet), Sperre wieder lösen.
            if (!HudConfirm.isOpen)
                _offerDialogOpen = false;
        }

        private void AcceptOffer(QuickRidMission mission)
        {
            _offerDialogOpen = false;

            if (mission.state != QuickRidTripState.Offered)
                return;

            ShowPassenger(_pickupPosition, _pickupRotation);
            PinPickup(mission);
            _mapFilter?.ShowPickup(_pickupPosition, mission.pickupAddress);

            mission.tripStartTime = TimeHelper.Now();
            mission.offerExpiryTime = null;
            mission.state = QuickRidTripState.PassengerWaiting;

            Notifications.Show(NotificationType.Info, "quickrid_passenger_waiting");
            _tasksUi?.UpdateUI();
        }

        private void DeclineOffer(QuickRidMission mission)
        {
            _offerDialogOpen = false;

            if (mission.state != QuickRidTripState.Offered)
                return;

            mission.ClearTrip();
            mission.nextRequestTime = CreateWaitDeadline();
            _tasksUi?.UpdateUI();
        }

        /// <remarks>
        /// Bewusst die Vector3-Überladung: <c>SetGuiderTarget(Address, …)</c> ruft
        /// <c>BuildingHelper.GetBuildingRegistration</c>, das für unbekannte Adressen einen neuen
        /// Eintrag im Spielstand anlegt. Bei einer zufälligen Wohnadresse alle paar Spielminuten
        /// müllt das den Save voll. Fürs Ziel ist die Address-Überladung dagegen richtig – dort ist
        /// der Gebäudename im POI erwünscht.
        /// </remarks>
        private void PinPickup(QuickRidMission mission)
        {
            if (mission.pickupAddress == null)
                return;

            GuidersManager.SetGuiderTarget(
                _pickupPosition,
                AddressHelper.ToFormattedString(mission.pickupAddress),
                InstanceBehavior<GlobalReferences>.Instance.vehiclePOIIcon,
                GuidersManager.GetGuiderColor(DirectionGuiderType.JobDestination),
                DirectionGuiderType.JobDestination);
        }

        /// <param name="ratingModifier">Bonus/Malus aus <see cref="QuickRidRating.CurrentModifier"/>.</param>
        /// <param name="tariffMultiplier">Zeitaufschlag aus <see cref="QuickRidTariff.Multiplier"/>.</param>
        private static float CalculateFare(float distance, float ratingModifier, float tariffMultiplier)
        {
            float raw = (FixedBaseFare + distance * FarePerMeter)
                * QuickRidSettings.FareMultiplier * ratingModifier * tariffMultiplier;
            return Mathf.Max(MinimumFare, Mathf.Round(raw));
        }

        /// <summary>Schicht-Ende, das nie eintritt – siehe <see cref="OpenEndDays"/>.</summary>
        private static Timestamp CreateOpenEndTime()
        {
            Timestamp endTime = TimeHelper.Now();
            endTime.AddDays(OpenEndDays);
            return endTime;
        }

        /// <remarks>
        /// Timestamp.AddMinutes verändert die Instanz und gibt sie zurück – deshalb immer von einem
        /// frischen TimeHelper.Now() ausgehen und nie von einem gespeicherten Feld.
        /// </remarks>
        private static Timestamp CreateWaitDeadline()
        {
            int min = Mathf.Min(QuickRidSettings.RequestWaitMinMinutes, QuickRidSettings.RequestWaitMaxMinutes);
            int max = Mathf.Max(QuickRidSettings.RequestWaitMinMinutes, QuickRidSettings.RequestWaitMaxMinutes);

            Timestamp deadline = TimeHelper.Now();
            deadline.AddMinutes(UnityEngine.Random.Range(min, max + 1));
            return deadline;
        }

        private static Timestamp CreateOfferDeadline()
        {
            Timestamp deadline = TimeHelper.Now();
            deadline.AddMinutes(QuickRidSettings.OfferTimeoutMinutes);
            return deadline;
        }

        // --- Adresskandidaten ----------------------------------------------------

        private void StartAddressCacheBuild()
        {
            StopAddressCacheBuild();
            _addressCandidates = null;
            _addressCacheRoutine = StartCoroutine(BuildAddressCacheRoutine());
        }

        private void StopAddressCacheBuild()
        {
            if (_addressCacheRoutine != null)
                StopCoroutine(_addressCacheRoutine);

            _addressCacheRoutine = null;
            _addressCacheReady = false;
        }

        /// <summary>
        /// Baut die Liste der Türen auf, an denen ein Auto überhaupt halten kann, und wärmt dabei
        /// den Fahrgast-Pool vor. Läuft einmal pro Stadtladen.
        /// </summary>
        /// <remarks>
        /// Der Filter fragt den Straßengraph des Spiels (Gley Traffic System): gibt es im Umkreis
        /// von <see cref="QuickRidSettings.RoadProximityMeters"/> einen Fahrspur-Waypoint, der Autos
        /// erlaubt, auf ähnlicher Höhe liegt und keine Kreuzungsverbindung ist? Dieselbe Prüfung
        /// benutzt das Spiel für sein eigenes Taxi (TaxiSystem.IsValidWaypointForLeaving) und den
        /// Fahrdienst (PrivateDriverHelpers.IsGoodStartWaypoint). Ein NavMesh hilft hier nicht: es
        /// kennt nur Fußgänger- und Spieler-Agenten, keine Fahrzeuge.
        /// <para>
        /// Jede Abfrage scannt alle Waypoints der Stadt, deshalb die Verteilung über Frames. Ein
        /// Coroutine-Start liegt in <c>RestoreState</c>; der Controller überlebt den Szenenwechsel
        /// (DontDestroyOnLoad), die Routine muss deshalb in <c>OnGameUnloaded</c> gestoppt werden.
        /// </para>
        /// </remarks>
        private IEnumerator BuildAddressCacheRoutine()
        {
            yield return new WaitUntil(() =>
                InstanceBehavior<CityManager>.IsInitialized && TrafficManager.IsInitialized);

            // Vier Fahrgäste bauen je ein Runtime-Mesh; in einem Frame gäbe das einen Hänger.
            while (!_passengers.IsComplete)
            {
                if (GameManager.isCitySceneBeingUnloaded)
                    yield break;

                _passengers.CreateOne(_context?.Logger);
                yield return null;
            }

            var list = new List<AddressCandidate>();
            CityBuildingController[] controllers = InstanceBehavior<CityManager>.Instance.cityBuildingControllers;

            int checkedDoors = 0;
            int rejected = 0;

            for (int i = 0; i < controllers.Length; i++)
            {
                if (i % AddressCacheBuildingsPerFrame == 0 && i > 0)
                {
                    yield return null;

                    if (GameManager.isCitySceneBeingUnloaded || !TrafficManager.IsInitialized)
                        yield break;
                }

                CityBuildingController controller = controllers[i];
                if (controller == null || controller.blockPedestrianSpawn || controller.building == null)
                    continue;
                if (controller.entranceDoors == null || controller.entranceDoors.Length == 0)
                    continue;

                BuildingEntranceDoor door = controller.entranceDoors[0];
                if (door == null || door.doorTransform == null)
                    continue;

                checkedDoors++;

                if (!IsNearDrivableRoad(door.doorTransform.position))
                {
                    rejected++;
                    continue;
                }

                list.Add(new AddressCandidate { address = controller.building.Address, door = door.doorTransform });
            }

            _addressCandidates = list;
            _addressCacheReady = true;

            QuickRidLog.Dev(_context?.Logger,
                $"QuickRid: {list.Count} von {checkedDoors} Türen liegen höchstens " +
                $"{QuickRidSettings.RoadProximityMeters:0} m von einer befahrbaren Straße entfernt " +
                $"({rejected} verworfen).");

            // Kippt der Filter mehr als die Hälfte weg, liegt das eher an der Schwelle als an der Stadt.
            if (checkedDoors > 0 && rejected > checkedDoors * AddressCacheRejectWarnRatio)
            {
                _context?.Logger.Warn(
                    $"QuickRid: {rejected} von {checkedDoors} Türen verworfen – " +
                    $"QuickRidSettings.RoadProximityMeters ({QuickRidSettings.RoadProximityMeters:0} m) " +
                    "ist vermutlich zu streng.");
            }

            _addressCacheRoutine = null;
        }

        /// <summary>Liegt neben dieser Tür ein Straßenpunkt, an dem ein Auto halten darf?</summary>
        /// <remarks>
        /// <c>temporaryDisabled</c> wird bewusst nicht geprüft: das Flag ist vorübergehend (etwa
        /// wegen anderer Fahrzeuge) und würde eine dauerhaft gültige Adresse zufällig aussortieren.
        /// </remarks>
        private static bool IsNearDrivableRoad(Vector3 doorPosition)
        {
            float doorHeight = doorPosition.y;

            Waypoint waypoint = TrafficManager.Instance.GetClosestWaypoint(
                doorPosition,
                QuickRidSettings.RoadProximityMeters,
                w => w.allowedCars.Count > 0
                    && TrafficManager.WithinHeight(w, doorHeight)
                    && TrafficManager.IsNotIntersection(w));

            return waypoint != null;
        }

        // --- Fahrgast ------------------------------------------------------------

        /// <summary>Zeigt einen zufälligen Fahrgast aus dem Pool samt Abholring.</summary>
        private void ShowPassenger(Vector3 position, Quaternion rotation)
        {
            if (!_passengers.Show(position, rotation, _context?.Logger))
                return;

            ShowPickupCircle(position);
        }

        private void HidePassenger()
        {
            _passengers.Hide();
            HidePickupCircle();
        }

        /// <summary>Räumt Fahrgäste, beide Ringe und das gemeinsame Material ab.</summary>
        private void DestroyVisuals()
        {
            if (GameManager.isCitySceneBeingUnloaded)
                return;

            _passengers.Destroy();

            if (_pickupRoot != null)
                Destroy(_pickupRoot);
            if (_dropoffRoot != null)
                Destroy(_dropoffRoot);
            if (_circleMaterial != null)
                Destroy(_circleMaterial);

            _pickupRoot = null;
            _pickupCircle = null;
            _dropoffRoot = null;
            _dropoffCircle = null;
            _circleMaterial = null;
        }

        // --- Abholzone -----------------------------------------------------------

        private void ShowPickupCircle(Vector3 position)
        {
            if (_circleCreationFailed)
                return;

            if (_pickupRoot == null)
            {
                var root = new GameObject("QuickRid - Pickup Area");
                _pickupCircle = CreateCircle("Pickup Area", root.transform);

                if (_pickupCircle == null)
                {
                    Destroy(root);
                    _circleCreationFailed = true;
                    return;
                }

                root.SetActive(false);
                _pickupRoot = root;
            }

            _pickupRoot.transform.position = position;
            UpdateCircle(_pickupCircle, QuickRidSettings.PickupRadiusMeters);
            _pickupRoot.SetActive(true);
        }

        private void HidePickupCircle()
        {
            if (_pickupRoot != null)
                _pickupRoot.SetActive(false);
        }

        // --- Absetzzone ----------------------------------------------------------

        /// <summary>
        /// Stellt den Ring am Ziel sicher, solange ein Fahrgast an Bord ist. Steht er schon, kostet
        /// der Aufruf nichts – deshalb darf er aus dem Frame-Tick kommen.
        /// </summary>
        /// <remarks>
        /// Beim Zustandswechsel ist die Zieladresse nicht immer sofort auflösbar (das Gebäude kann
        /// noch nicht bereitstehen). Der Ring entsteht deshalb beim ersten Tick, an dem
        /// <c>GetAddressEntranceTransform</c> etwas liefert, und nicht einmalig beim Einsteigen.
        /// </remarks>
        private void EnsureDropoffCircle(QuickRidMission mission)
        {
            if (_dropoffRoot != null && _dropoffRoot.activeSelf)
                return;

            if (mission.destinationAddress == null)
                return;

            Transform entrance = BuildingHelper.GetAddressEntranceTransform(mission.destinationAddress);
            if (entrance == null)
                return;

            ShowDropoffCircle(entrance.position);
        }

        private void ShowDropoffCircle(Vector3 position)
        {
            if (_circleCreationFailed)
                return;

            if (_dropoffRoot == null)
            {
                var root = new GameObject("QuickRid - Dropoff Area");
                _dropoffCircle = CreateCircle("Dropoff Area", root.transform);

                if (_dropoffCircle == null)
                {
                    Destroy(root);
                    _circleCreationFailed = true;
                    return;
                }

                root.SetActive(false);
                _dropoffRoot = root;
            }

            _dropoffRoot.transform.position = position;
            UpdateCircle(_dropoffCircle, QuickRidSettings.DropoffRadiusMeters);
            _dropoffRoot.SetActive(true);
        }

        private void HideDropoffCircle()
        {
            if (_dropoffRoot != null)
                _dropoffRoot.SetActive(false);
        }

        // --- Ringe ---------------------------------------------------------------

        /// <summary>Flacher Ring am Boden in der QuickRid-Farbe. Radius setzt <see cref="UpdateCircle"/>.</summary>
        private LineRenderer? CreateCircle(string name, Transform parent)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                _context?.Logger.Warn("QuickRid: Shader \"Sprites/Default\" nicht gefunden – kein Ring.");
                return null;
            }

            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.08f;

            LineRenderer line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = PickupCircleSegments;
            line.startWidth = 0.12f;
            line.endWidth = 0.12f;

            // Dieselbe Farbe wie der aktive Online-Button, damit alles als "QuickRid" lesbar ist.
            // Ein Material für beide Ringe; freigegeben wird es erst in OnDestroy.
            if (_circleMaterial == null)
                _circleMaterial = new Material(shader) { color = Colors.Lime };

            line.sharedMaterial = _circleMaterial;
            return line;
        }

        private static void UpdateCircle(LineRenderer? circle, float radius)
        {
            if (circle == null)
                return;

            int count = circle.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                circle.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        // --- Fahrzeug ------------------------------------------------------------

        /// <remarks>
        /// Ein in einem Gebäude geparktes Auto steht nicht in AllPlayerVehicles – dann bleibt
        /// _missionCar null und der Tick tut nichts. Das ist gewollt: offline gehen wäre falsch,
        /// der Spieler ist vielleicht nur kurz woanders.
        /// </remarks>
        private void RebindMissionCar(QuickRidMission mission)
        {
            if (_missionCar != null && _missionCar.vehicleInstance != null
                && _missionCar.vehicleInstance.id == mission.vehicleId)
                return;

            _missionCar = null;
            _missionCarBody = null;

            if (mission.vehicleId == null)
                return;

            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                VehicleController vehicle = all[i];
                if (vehicle == null || vehicle.vehicleInstance == null || vehicle.vehicleInstance.id != mission.vehicleId)
                    continue;

                _missionCar = vehicle as CarController;
                // VehicleController._rigidbody ist privat; das Spiel greift an anderer Stelle selbst
                // per GetComponent zu (SkipBridgeHelper). Einmal beim Binden cachen genügt.
                _missionCarBody = _missionCar != null ? _missionCar.GetComponent<Rigidbody>() : null;
                return;
            }
        }

        private static bool IsDrivingMissionCar(QuickRidMission mission)
        {
            VehicleInstance current = VehicleHelper.GetCurrentVehicle();
            return current != null && mission.vehicleId != null && current.id == mission.vehicleId;
        }

        /// <summary>Steht das Auto oder rollt es höchstens Schrittgeschwindigkeit?</summary>
        /// <remarks>
        /// Über den gecachten Rigidbody, nicht über <c>CarController.CurrentSpeed</c>: das ist
        /// <c>Mathf.Round(vehicleController.Speed)</c> und damit auf 1 m/s gerundet – eine Schwelle
        /// von 1,5 m/s wäre so gar nicht darstellbar.
        /// </remarks>
        private bool IsMissionCarStopped()
        {
            if (_missionCarBody == null)
                return true; // ohne Rigidbody nicht blockieren

            return _missionCarBody.velocity.sqrMagnitude < WalkingSpeed * WalkingSpeed;
        }

        // --- Button im Fahrzeug-Panel --------------------------------------------

        private void OnEnterVehicle(VehicleController vehicle)
        {
            // CarController hat im Spielcode keine Unterklassen; ScooterController und HandTruck
            // leiten direkt von VehicleController ab und fallen damit heraus.
            _currentCar = vehicle is CarController car && IsOwnedByPlayer(car) ? car : null;

            if (_currentCar == null)
            {
                SetJobButtonVisible(false);
                return;
            }

            EnsureJobButton();
            UpdateJobButton();
        }

        private void OnExitVehicle(VehicleController vehicle)
        {
            _currentCar = null;
            SetJobButtonVisible(false);
        }

        /// <remarks>
        /// Klon des vanilla "Parken"-Buttons, direkt daneben einsortiert. Der geklonte Button bringt den
        /// Park-Listener mit – deshalb muss onClick komplett ersetzt und nicht nur ergänzt werden.
        /// </remarks>
        private void EnsureJobButton()
        {
            if (_jobButton != null)
                return;

            Button parkButton = InstanceBehavior<UIs>.Instance.playerHUD.itemPanelUI.parkButton;
            if (parkButton == null)
            {
                _context?.Logger.Warn("QuickRid: ItemPanelUI.parkButton nicht gefunden – kein Job-Button im Fahrzeug-Panel.");
                return;
            }

            _jobButton = Instantiate(parkButton, parkButton.transform.parent);
            _jobButton.name = ButtonName;
            _jobButton.onClick = new Button.ButtonClickedEvent();
            _jobButton.onClick.AddListener(OnClickJobButton);
            _jobButton.transform.SetSiblingIndex(parkButton.transform.GetSiblingIndex());

            _jobButtonLabel = _jobButton.transform.GetLanguageChangeEventByName("Label");
            _jobButtonLabel.Suffix = string.Empty;

            _defaultColors = _jobButton.colors;
            if (_jobButton.targetGraphic != null)
                _defaultGraphicColor = _jobButton.targetGraphic.color;
            _colorsCaptured = true;

            _jobButton.gameObject.SetActive(false);

            // onPause feuert nur bei Wechsel; der Button entsteht erst beim Einsteigen.
            ApplyPausedState(InstanceBehavior<UIs>.Instance.gameSpeed.Paused);
        }

        /// <remarks>
        /// Spiegelt ItemPanelUI.OnPaused: der geklonte Button soll bei pausiertem Spiel genauso
        /// ausgegraut sein wie das benachbarte "Parken", im Platzierungsmodus aber bedienbar bleiben.
        /// </remarks>
        private void OnPaused(bool paused)
        {
            ApplyPausedState(paused);
        }

        private void ApplyPausedState(bool paused)
        {
            if (PlacementSystem.IsInPlacementMode)
                paused = false;

            if (_jobButton != null)
                _jobButton.interactable = !paused;
        }

        /// <summary>
        /// Sichtbar nur im eigenen Auto: "Online gehen" wenn gar keine Mission läuft, "Offline gehen"
        /// wenn die eigene Schicht mit genau diesem Auto läuft. Bei einer fremden Mission (z. B.
        /// Lieferfahrer) bleibt der Button weg.
        /// </summary>
        private void UpdateJobButton()
        {
            if (_jobButton == null || _jobButtonLabel == null)
                return;

            if (_currentCar == null || _currentCar.vehicleInstance == null || SaveGameManager.Current == null)
            {
                SetJobButtonVisible(false);
                return;
            }

            QuickRidMission? mission = Mission;
            bool online = mission != null;
            bool show = online
                ? mission!.vehicleId == _currentCar.vehicleInstance.id
                : SaveGameManager.Current.currentPlayerMission == null;

            SetJobButtonVisible(show);

            if (!show)
                return;

            _jobButtonLabel.Key = online ? "quickrid_go_offline" : "quickrid_go_online";
            ApplyJobButtonColors(online);
        }

        /// <summary>
        /// Offline: Standardfarben des Park-Buttons. Online: <c>Colors.Lime</c> aus der Spiel-Palette,
        /// damit der aktive Zustand sofort auffaellt.
        /// </summary>
        /// <remarks>
        /// Die Palette (Colors.cs) fuehrt drei Gruenstufen: darkGreen, green und lime. Dass lime die
        /// hellere Variante von green ist, zeigt SecurityActionPanelUi: rot -> orange -> green -> lime.
        /// <para>
        /// disabledColor, colorMultiplier und fadeDuration bleiben unangetastet aus dem Original –
        /// so graut ItemPanelUI.OnPaused den Button weiterhin genau wie "Parken" aus (dunkelblau).
        /// </para>
        /// </remarks>
        private void ApplyJobButtonColors(bool online)
        {
            if (_jobButton == null || !_colorsCaptured || _jobButton.targetGraphic == null)
                return;

            if (!online)
            {
                _jobButton.colors = _defaultColors;
                _jobButton.targetGraphic.color = _defaultGraphicColor;
                return;
            }

            ColorBlock block = _defaultColors;
            block.normalColor      = Color.white;
            block.selectedColor    = Color.white;
            block.highlightedColor = new Color(1f, 1f, 1f, 1f) * 1.2f;   // leicht heller bei Hover
            block.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f); // leicht dunkler bei Klick
            // disabledColor bleibt aus dem Original → Pause graut weiterhin aus

            _jobButton.colors = block;
            _jobButton.targetGraphic.color = Colors.Lime;
        }

        private void SetJobButtonVisible(bool visible)
        {
            if (_jobButton != null)
                _jobButton.gameObject.SetActive(visible);
        }

        private void OnClickJobButton()
        {
            if (Mission != null)
                PromptGoOffline();
            else
                RequestGoOnline();
        }

        private static bool IsOwnedByPlayer(CarController car)
        {
            if (car.vehicleInstance == null || SaveGameManager.Current == null)
                return false;

            List<VehicleInstance> owned = SaveGameManager.Current.VehicleInstances;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].id == car.vehicleInstance.id)
                    return true;
            }

            return false;
        }

        // --- Adresse ausschließen ------------------------------------------------

        /// <summary>
        /// Adresse der laufenden Fahrt, die der Ausschluss-Button meldet: die Abholung, solange der
        /// Fahrgast wartet, sonst das Ziel. Null, wenn gerade nichts zu melden ist.
        /// </summary>
        private Address? GetExcludableAddress()
        {
            QuickRidMission? mission = Mission;
            if (mission == null)
                return null;

            switch (mission.state)
            {
                case QuickRidTripState.PassengerWaiting:
                    return mission.pickupAddress;
                case QuickRidTripState.PassengerAboard:
                    return mission.destinationAddress;
                default:
                    return null;
            }
        }

        /// <summary>Bestätigungsdialog des Ausschluss-Buttons im Aufgabenpanel.</summary>
        public void PromptExcludeAddress()
        {
            QuickRidMission? mission = Mission;
            Address? address = GetExcludableAddress();
            if (mission == null || address == null || HudConfirm.isOpen)
                return;

            HudConfirm.Show(
                new LanguageChangeEventDataHolder { Key = "quickrid_exclude_title" },
                new LanguageChangeEventDataHolder
                {
                    Key = "quickrid_exclude_body",
                    Arguments = new { address = AddressHelper.ToFormattedString(address) }
                },
                () => ExcludeAddress(mission, address),
                null,
                "quickrid_exclude_confirm",
                "quickrid_decline_job",
                false);
        }

        /// <summary>
        /// Trägt die Adresse dauerhaft in die Sperrliste ein und bricht die Fahrt ab – ohne
        /// Sternabzug, denn gemeldet wird ein Fehler der Mod, keine Entscheidung des Spielers.
        /// </summary>
        private void ExcludeAddress(QuickRidMission mission, Address address)
        {
            // Der Dialog blockiert das Spiel nicht: bis zur Bestätigung kann die Fahrt vorbei sein.
            if (!ReferenceEquals(Mission, mission))
                return;
            if (mission.state != QuickRidTripState.PassengerWaiting && mission.state != QuickRidTripState.PassengerAboard)
                return;

            QuickRidBlacklist.Add(address, QuickRidBlacklist.SourceManual);

            mission.excludedAddresses++;
            QuickRidStats.SaveFrom(mission);

            AbortTrip(mission);

            string formatted = AddressHelper.ToFormattedString(address);
            Notifications.Show(NotificationType.Info, "quickrid_address_excluded",
                new Dictionary<string, string> { { "address", formatted } });

            _context?.Logger.Info($"QuickRid: Adresse ausgeschlossen – {formatted} " +
                $"({QuickRidBlacklist.Count} gesperrt, {mission.excludedAddresses} Meldungen insgesamt).");
        }

        // --- Online / Offline ----------------------------------------------------

        private void RequestGoOnline()
        {
            if (SaveGameManager.Current.currentPlayerMission != null)
            {
                Notifications.ShowError("notification_already_ongoing_mission");
                return;
            }

            if (PlayerHelper.IsHoldingItem)
            {
                Notifications.ShowError("notification_need_empty_hands_to_interact");
                return;
            }

            CarController? car = _currentCar;
            if (car == null || HudConfirm.isOpen)
                return;

            HudConfirm.Show(
                "quickrid_job_title",
                "quickrid_start_job",
                () => GoOnline(car),
                null,
                "quickrid_accept_job",
                "quickrid_decline_job");
        }

        /// <summary>Bestätigungsdialog vor dem Offline-Gehen – für den Fahrzeug-Button und das Aufgabenpanel.</summary>
        public void PromptGoOffline()
        {
            QuickRidMission? mission = Mission;
            if (mission == null || HudConfirm.isOpen)
                return;

            // Mit Fahrgast an Bord kostet Offline gehen einen Stern – das muss im Dialog stehen.
            string bodyKey = mission.state == QuickRidTripState.PassengerAboard
                ? "quickrid_go_offline_confirm_passenger"
                : "quickrid_go_offline_confirm";

            HudConfirm.Show(
                "quickrid_job_title",
                bodyKey,
                GoOffline,
                null,
                "quickrid_go_offline",
                "quickrid_decline_job");
        }

        public void GoOnline(CarController car)
        {
            if (car == null || car.vehicleInstance == null || SaveGameManager.Current.currentPlayerMission != null)
                return;

            var mission = new QuickRidMission
            {
                vehicleId = car.vehicleInstance.id,
                startTime = TimeHelper.Now(),
                endTime = CreateOpenEndTime(),
                timeLimitMinutes = 0,
                state = QuickRidTripState.Waiting,
                nextRequestTime = CreateWaitDeadline()
            };
            QuickRidStats.LoadInto(mission);

            SaveGameManager.Current.currentPlayerMission = mission;

            if (InstanceBehavior<UIs>.Instance.tasksUI.IsCollapsed)
                InstanceBehavior<UIs>.Instance.tasksUI.SetCollapsedState(false);

            _tasksUi?.Init();
            UpdateJobButton();
            _context?.Logger.Info("QuickRid: driver online.");
        }

        public void GoOffline()
        {
            QuickRidMission? mission = Mission;
            if (mission == null)
                return;

            // Wer mit Fahrgast an Bord offline geht, lässt ihn sitzen: 1 Stern, kein Fahrpreis.
            if (mission.state == QuickRidTripState.PassengerAboard)
            {
                QuickRidRating.Push(mission.GetRatingHistory(), 1);
                Notifications.Show(NotificationType.Warning, "quickrid_trip_abandoned");
                _context?.Logger.Info("QuickRid: Fahrgast beim Offline-Gehen im Stich gelassen – 1 Stern.");
            }

            // Die Mission wird gleich gelöscht; Historie und Zähler überleben nur in modData.
            QuickRidStats.SaveFrom(mission);

            // Fahrgast, Ringe und Kartenpin gehören zur Schicht – alles muss mit ihr verschwinden.
            HidePassenger();
            HideDropoffCircle();
            GuidersManager.ResetGuider(DirectionGuiderType.JobDestination);
            _mapFilter?.Clear();
            _offerDialogOpen = false;

            SaveGameManager.Current.currentPlayerMission = null;
            _tasksUi?.Hide();
            UpdateJobButton();
            _context?.Logger.Info("QuickRid: driver offline.");

            // Erst ganz zum Schluss: die Übersicht belegt den Missions-Slot kurzzeitig selbst.
            QuickRidSessionSummary.Show(mission, _context?.Logger);
        }
    }
}
