using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    partial class TimelineWindow
    {
        static readonly GUIContent[] k_EditModes = new GUIContent[3];
        static readonly GUIContent[] k_TimeReferenceGUIContents =
        {
            EditorGUIUtility.TrTextContent("Local", "Display time based on the current timeline."),
            EditorGUIUtility.TrTextContent("Global", "Display time based on the master timeline.")
        };

        TimelineMarkerHeaderGUI m_MarkerHeaderGUI;

        void SequencerHeaderGUI()
        {
            using (new EditorGUI.DisabledScope(state.editSequence.asset == null))
            {
                GUILayout.BeginVertical();
                {
                    TransportToolbarGUI();

                    GUILayout.BeginHorizontal(GUILayout.Width(sequenceHeaderRect.width));
                    {
                        if (state.editSequence.asset != null)
                        {
                            GUILayout.Space(DirectorStyles.kBaseIndent);
                            AddButtonGUI();
                            GUILayout.FlexibleSpace();
                            EditModeToolbarGUI();
                            ShowMarkersButton();
                            EditorGUILayout.Space();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
        }

        void MarkerHeaderGUI()
        {
            var timelineAsset = state.editSequence.asset;
            if (timelineAsset == null)
                return;

            if (m_MarkerHeaderGUI == null)
                m_MarkerHeaderGUI = new TimelineMarkerHeaderGUI(timelineAsset, state);
            m_MarkerHeaderGUI.Draw(markerHeaderRect, markerContentRect, state);
        }

        void TransportToolbarGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbarButton, GUILayout.Width(sequenceHeaderRect.width));
            {
                using (new EditorGUI.DisabledScope(currentMode.PreviewState(state) == TimelineModeGUIState.Disabled))
                {
                    PreviewModeButtonGUI();
                }

                using (new EditorGUI.DisabledScope(currentMode.ToolbarState(state) == TimelineModeGUIState.Disabled))
                {
                    GotoBeginingSequenceGUI();
                    PreviousEventButtonGUI();
                    PlayButtonGUI();
                    NextEventButtonGUI();
                    GotoEndSequenceGUI();
                    GUILayout.Space(10.0f);
                    PlayRangeButtonGUI();
                    GUILayout.FlexibleSpace();
                    TimeCodeGUI();
                    ReferenceTimeGUI();
                }
            }
            GUILayout.EndHorizontal();
        }

        void PreviewModeButtonGUI()
        {
            EditorGUI.BeginChangeCheck();
            var enabled = state.previewMode;
            enabled = GUILayout.Toggle(enabled, DirectorStyles.previewContent, EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                // turn off auto play as well, so it doesn't auto reenable
                if (!enabled)
                    state.SetPlaying(false);

                state.previewMode = enabled;

                // if we are successfully enabled, rebuild the graph so initial states work correctly
                // Note: testing both values because previewMode setter can "fail"
                if (enabled && state.previewMode)
                    state.rebuildGraph = true;
            }
        }

        void GotoBeginingSequenceGUI()
        {
            if (GUILayout.Button(DirectorStyles.gotoBeginingContent, EditorStyles.toolbarButton))
            {
                state.editSequence.time = 0;
                state.EnsurePlayHeadIsVisible();
            }
        }

        // in the editor the play button starts/stops simulation
        void PlayButtonGUIEditor()
        {
            EditorGUI.BeginChangeCheck();
            var isPlaying = GUILayout.Toggle(state.playing, DirectorStyles.playContent, EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                state.SetPlaying(isPlaying);
            }
        }

        // in playmode the button reflects the playing state.
        //  needs to disabled if playing is not possible
        void PlayButtonGUIPlayMode()
        {
            bool buttonEnabled = state.masterSequence.director != null &&
                state.masterSequence.director.isActiveAndEnabled;
            using (new EditorGUI.DisabledScope(!buttonEnabled))
            {
                PlayButtonGUIEditor();
            }
        }

        void PlayButtonGUI()
        {
            if (!Application.isPlaying)
                PlayButtonGUIEditor();
            else
                PlayButtonGUIPlayMode();
        }

        void NextEventButtonGUI()
        {
            if (GUILayout.Button(DirectorStyles.nextFrameContent, EditorStyles.toolbarButton))
            {
                state.referenceSequence.frame += 1;
            }
        }

        void PreviousEventButtonGUI()
        {
            if (GUILayout.Button(DirectorStyles.previousFrameContent, EditorStyles.toolbarButton))
            {
                state.referenceSequence.frame -= 1;
            }
        }

        void GotoEndSequenceGUI()
        {
            if (GUILayout.Button(DirectorStyles.gotoEndContent, EditorStyles.toolbarButton))
            {
                state.editSequence.time = state.editSequence.asset.duration;
                state.EnsurePlayHeadIsVisible();
            }
        }

        void PlayRangeButtonGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || state.IsEditingASubTimeline()))
            {
                state.playRangeEnabled = GUILayout.Toggle(state.playRangeEnabled, DirectorStyles.Instance.playrangeContent, EditorStyles.toolbarButton);
            }
        }

        void AddButtonGUI()
        {
            if (currentMode.trackOptionsState.newButton == TimelineModeGUIState.Hidden)
                return;

            using (new EditorGUI.DisabledScope(currentMode.trackOptionsState.newButton == TimelineModeGUIState.Disabled))
            {
                if (EditorGUILayout.DropdownButton(DirectorStyles.newContent, FocusType.Passive, "Dropdown"))
                {
                    // if there is 1 and only 1 track selected, AND it's a group, add to that group
                    TrackAsset parent = null;
                    var groupTracks = SelectionManager.SelectedTracks().ToList();
                    if (groupTracks.Count == 1)
                    {
                        parent = groupTracks[0] as GroupTrack;
                        // if it's locked, add to the root instead
                        if (parent != null && parent.lockedInHierarchy)
                            parent = null;
                    }
                    SequencerContextMenu.ShowNewTracksContextMenu(parent, null, state);
                }
            }
        }

        void ShowMarkersButton()
        {
            var asset = state.editSequence.asset;
            if (asset == null) return;

            var content = state.showMarkerHeader ? DirectorStyles.showMarkersOn : DirectorStyles.showMarkersOff;
            state.showMarkerHeader = GUILayout.Toggle(state.showMarkerHeader, content, GUI.skin.button);
        }

        static void EditModeToolbarGUI()
        {
            var editType = EditMode.editType;

            k_EditModes[0] = editType == EditMode.EditType.Mix ? DirectorStyles.mixOn : DirectorStyles.mixOff;
            k_EditModes[1] = editType == EditMode.EditType.Ripple ? DirectorStyles.rippleOn : DirectorStyles.rippleOff;
            k_EditModes[2] = editType == EditMode.EditType.Replace ? DirectorStyles.replaceOn : DirectorStyles.replaceOff;

            var newEditType = (EditMode.EditType)GUILayout.Toolbar((int)editType, k_EditModes, DirectorStyles.Instance.editModesToolbar);
            if (newEditType != editType)
                EditMode.editType = newEditType;
        }

        // Draws the box to enter the time field
        void TimeCodeGUI()
        {
            EditorGUI.BeginChangeCheck();

            var currentTime = state.editSequence.asset != null ? TimeReferenceUtility.ToTimeString(state.editSequence.time, "F1") : "0";
            var r = EditorGUILayout.GetControlRect(false, EditorGUI.kSingleLineHeight, EditorStyles.toolbarTextField, GUILayout.MinWidth(WindowConstants.minTimeCodeWidth));
            var id = GUIUtility.GetControlID("RenameFieldTextField".GetHashCode(), FocusType.Passive, r);
            var newCurrentTime = EditorGUI.DelayedTextFieldInternal(r, id, GUIContent.none, currentTime, null, EditorStyles.toolbarTextField);

            if (EditorGUI.EndChangeCheck())
                state.editSequence.time = TimeReferenceUtility.FromTimeString(newCurrentTime);
        }

        void ReferenceTimeGUI()
        {
            EditorGUI.BeginChangeCheck();

            var rect = EditorGUILayout.GetControlRect(false, EditorGUI.kSingleLineHeight, EditorStyles.toolbarButton, GUILayout.Width(WindowConstants.refTimeWidth));
            state.timeReferenceMode = (TimeReferenceMode)EditorGUI.CycleButton(rect, (int)state.timeReferenceMode, k_TimeReferenceGUIContents, EditorStyles.toolbarButton);

            if (EditorGUI.EndChangeCheck())
                OnTimeReferenceModeChanged();
        }

        void OnTimeReferenceModeChanged()
        {
            m_TimeAreaDirty = true;
            InitTimeAreaFrameRate();
            SyncTimeAreaShownRange();

            foreach (var inspector in InspectorWindow.GetAllInspectorWindows())
            {
                inspector.Repaint();
            }
        }
    }
}