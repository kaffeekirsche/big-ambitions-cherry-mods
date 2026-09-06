#nullable enable
using System;
using System.Reflection;
using BigAmbitions.ModsInternal;
using Localizor;
using Localizor.LanguageChangeEvent;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CherryQuickRid
{
    /// <summary>
    /// Schreibt die Beschriftung des Sperrlisten-Buttons im Optionsmenü.
    /// </summary>
    /// <remarks>
    /// <c>ModOptions.AddButton</c> nimmt zwar einen Locale-Key entgegen, die gerenderte Schaltfläche
    /// zeigt aber einen Platzhalter („YOUR TEXT HERE") statt des Textes – der Key landet nur auf der
    /// Beschriftung der Zeile, nicht auf dem Knopf. Diese Klasse setzt den Knopftext nach.
    /// Vorlage: TaxiOptionsUiFixer in _reference/BeATaxi~.
    /// <para>
    /// Es gibt kein Ereignis für „Optionsbildschirm geöffnet": <c>OptionsService.OnChanged</c> feuert
    /// nur beim Registrieren und Entfernen, die Steuerelemente entstehen aber erst beim Öffnen des
    /// Panels. Deshalb ein Blick pro Frame, der nur dann etwas tut, wenn das Panel sichtbar ist und
    /// der Text noch nicht gesetzt wurde.
    /// </para>
    /// </remarks>
    public sealed class QuickRidOptionsUiFixer : MonoBehaviour
    {
        /// <summary>Locale-Key, mit dem der Button registriert wurde – dient als Erkennungsmerkmal.</summary>
        private const string ButtonLabelKey = "quickrid_blacklist_reset";

        /// <summary>Locale-Key des Textes, der auf dem Knopf stehen soll.</summary>
        private const string ButtonTextKey = "quickrid_blacklist_reset_button";

        private const string TextMeshProTypeName = "TMPro.TextMeshProUGUI";

        private ModOptionsViewController? _optionsView;

        /// <summary>Gesetzt, sobald der Text für die aktuelle Anzeige des Panels geschrieben wurde.</summary>
        private bool _applied;

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            BindOptionsView();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _optionsView = null;
            _applied = false;
            BindOptionsView();
        }

        private void BindOptionsView()
        {
            _optionsView = FindObjectOfType<ModOptionsViewController>(true);
        }

        private void LateUpdate()
        {
            // Gesucht wird nur beim Szenenwechsel, nicht pro Frame: FindObjectOfType ist teuer.
            if (_optionsView == null)
            {
                _applied = false;
                return;
            }

            if (!_optionsView.gameObject.activeInHierarchy)
            {
                // Beim nächsten Öffnen werden die Steuerelemente neu aufgebaut – dann erneut setzen.
                _applied = false;
                return;
            }

            if (_applied)
                return;

            _applied = TryApplyButtonText();
        }

        private bool TryApplyButtonText()
        {
            if (_optionsView == null)
                return false;

            ModOptionsButtonControl[] controls = _optionsView.GetComponentsInChildren<ModOptionsButtonControl>(true);

            for (int i = 0; i < controls.Length; i++)
            {
                ModOptionsButtonControl control = controls[i];
                if (control == null || !HasLabelKey(control))
                    continue;

                Button button = control.GetComponentInChildren<Button>(true);
                if (button == null)
                    continue;

                return TrySetText(button.transform, ButtonTextKey.GetLocalization());
            }

            return false;
        }

        /// <summary>
        /// Erkennt die eigene Zeile am Locale-Key ihrer Beschriftung. Der bleibt auch dann gesetzt,
        /// wenn der sichtbare Text ein Platzhalter ist.
        /// </summary>
        private static bool HasLabelKey(ModOptionsButtonControl control)
        {
            TextLocalizationComponent[] labels = control.GetComponentsInChildren<TextLocalizationComponent>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].Key == ButtonLabelKey)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Setzt den Text der TextMeshPro-Komponente unter dem Knopf.
        /// </summary>
        /// <remarks>
        /// Über Reflection, weil TextMeshPro in der asmdef nicht referenziert ist – dasselbe Muster
        /// wie in <see cref="QuickRidSessionSummary"/>.
        /// </remarks>
        private static bool TrySetText(Transform root, string text)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().FullName != TextMeshProTypeName)
                    continue;

                PropertyInfo? property = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanWrite)
                    return false;

                try
                {
                    property.SetValue(component, text);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }
    }
}
