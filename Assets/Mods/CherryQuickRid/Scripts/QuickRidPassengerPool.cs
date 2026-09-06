#nullable enable
using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UnityEngine;

namespace CherryQuickRid
{
    /// <summary>
    /// Kleiner Vorrat an Fahrgästen mit je einmal gewürfeltem Aussehen; pro Fahrt wird zufällig
    /// einer gezeigt. Vorbild: <c>BaseHumanPool</c> im Spiel (Aussehen nur beim Anlegen würfeln,
    /// Get = aktivieren, Release = deaktivieren, Destroy = Objekt zerstören).
    /// </summary>
    /// <remarks>
    /// Das Aussehen wird bewusst nur einmal je Instanz gewürfelt: <c>AppearanceSetter.UpdateVisuals</c>
    /// lässt den SkinnedMeshCombiner (Fremdcode ohne Quelle) ein Runtime-Mesh bauen, und ob ein
    /// erneutes Würfeln das alte freigibt, ist nicht nachprüfbar (siehe IDEEN.md, Stufe 3).
    /// Alle Instanzen hängen unter einem Root in der Stadt-Szene; beim Stadtverlassen zerstört die
    /// Szene sie, dann genügt <see cref="ForgetSceneObjects"/>.
    /// </remarks>
    public sealed class QuickRidPassengerPool
    {
        public const int PoolSize = 4;

        /// <summary>
        /// Vanilla-Prefab für stehende NPCs. Bewusst nicht "Characters/HumanDefinitionLow": das ist
        /// ein <c>ThirdPersonCharacter</c> mit Update-Schleife, NavMeshAgent und Rigging.
        /// </summary>
        private const string PassengerPrefab = "Characters/DummyHuman";

        private const string RootName = "QuickRid - Passengers";

        private GameObject? _root;
        private readonly List<BaseHuman> _humans = new List<BaseHuman>();
        private BaseHuman? _active;
        private bool _spawnFailed;

        public int Count => _humans.Count;

        /// <summary>Alle Instanzen angelegt – oder das Anlegen ist endgültig gescheitert.</summary>
        public bool IsComplete => _spawnFailed || _humans.Count >= PoolSize;

        /// <summary>
        /// Legt genau eine Instanz an, damit der Aufrufer die Mesh-Aufbauten über mehrere Frames
        /// verteilen kann. False nur bei einem Fehler; danach wird nicht mehr versucht.
        /// </summary>
        public bool CreateOne(IModLogger? logger)
        {
            if (_spawnFailed)
                return false;
            if (_humans.Count >= PoolSize)
                return true;

            if (_root == null)
                _root = new GameObject(RootName);

            try
            {
                BaseHuman human = PrefabHelper.CreatePrefab<BaseHuman>(PassengerPrefab, _root.transform);
                human.gameObject.SetActive(true); // das Prefab wird inaktiv ausgeliefert
                human.appearanceSetter.SetRandomAppearance();
                human.name = "Passenger " + (_humans.Count + 1);
                human.transform.localPosition = Vector3.zero;
                human.transform.localRotation = Quaternion.identity;
                human.gameObject.SetActive(false);
                _humans.Add(human);
                return true;
            }
            catch (Exception exception)
            {
                _spawnFailed = true;
                logger?.Error("QuickRid: Fahrgast-Prefab konnte nicht erzeugt werden: " + exception.Message);
                return false;
            }
        }

        /// <summary>Legt fehlende Instanzen sofort an. True, sobald mindestens ein Fahrgast existiert.</summary>
        public bool EnsureCreated(IModLogger? logger)
        {
            while (!IsComplete)
                CreateOne(logger);

            return _humans.Count > 0;
        }

        /// <summary>Zeigt einen zufälligen Fahrgast an der Position; ein vorher sichtbarer verschwindet.</summary>
        public bool Show(Vector3 position, Quaternion rotation, IModLogger? logger)
        {
            if (!EnsureCreated(logger))
                return false;

            Hide();

            BaseHuman human = _humans[UnityEngine.Random.Range(0, _humans.Count)];
            if (human == null)
                return false;

            human.transform.SetPositionAndRotation(position, rotation);
            human.gameObject.SetActive(true);
            _active = human;
            return true;
        }

        public void Hide()
        {
            if (_active != null)
                _active.gameObject.SetActive(false);
            _active = null;
        }

        /// <summary>Zerstört den Root samt Fahrgästen (Controller.OnDestroy).</summary>
        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            ForgetSceneObjects();
        }

        /// <summary>Nur Referenzen vergessen – die Szene hat die Objekte bereits zerstört (onGameUnloaded).</summary>
        public void ForgetSceneObjects()
        {
            _root = null;
            _humans.Clear();
            _active = null;
            _spawnFailed = false;
        }
    }
}
