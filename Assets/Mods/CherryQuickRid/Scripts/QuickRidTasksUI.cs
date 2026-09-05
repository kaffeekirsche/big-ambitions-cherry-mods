#nullable enable
using Localizor.LanguageChangeEvent;
using UI;
using UI.Tasks;

namespace CherryQuickRid
{
    /// <summary>
    /// Eintrag im Aufgabenpanel, solange der Spieler online ist.
    /// Vorlage: _reference/BeATaxi~/BeATaxi/TaxiTasksUI.cs
    /// </summary>
    /// <remarks>
    /// Die Basisklasse pollt alle 0,3 s und ruft selbst <c>Hide()</c>, sobald im Missions-Slot
    /// keine <see cref="QuickRidMission"/> mehr liegt – Offline gehen muss die UI also nicht abräumen.
    /// </remarks>
    internal sealed class QuickRidTasksUI : MissionTasksUI<QuickRidMission>
    {
        private readonly QuickRidController _controller;

        public QuickRidTasksUI(QuickRidController controller)
        {
            _controller = controller;
        }

        public void Init()
        {
            UpdateUI();
            StartUpdateRoutine();
        }

        public override void UpdateUI()
        {
            if (!TryGetMission(out _))
                return;

            // Stufe 2 hat keinen dynamischen Inhalt: Panel einmal aufbauen, Text einmal setzen.
            if (tasksGroup != null)
                return;

            CreateUI();

            if (timeLabel != null)
                timeLabel.SetData(new LanguageChangeEventDataHolder { Key = "quickrid_tasks_wait" });
        }

        /// <remarks>
        /// CreateTimeEntry blendet Checkmark und DestinationButton selbst aus und verdrahtet den
        /// CloseButton mit <see cref="OnClickCancelJob"/>. Anders als Be A Taxi lassen wir diesen
        /// Button sichtbar – er ist unser "Offline gehen".
        /// </remarks>
        private void CreateUI()
        {
            CreateTasksGroup("quickrid_tasks_header");
            CreateTimeEntry();
            InstanceBehavior<UIs>.Instance.tasksUI.ScheduleUpdateObjectivesHeight();
        }

        protected override void OnClickCancelJob()
        {
            if (HudConfirm.isOpen)
                return;

            HudConfirm.Show(
                "quickrid_job_title",
                "quickrid_go_offline_confirm",
                _controller.GoOffline,
                null,
                "quickrid_go_offline",
                "quickrid_decline_job");
        }
    }
}
