using UnityEngine;
using UnityEditor;

namespace SAP.MRS.SpectatorView
{
    [CustomEditor(typeof(OcclusionObserver))]
    public class OcclusionObserverEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (!EditorApplication.isPlaying)
                return;

            var script = (OcclusionObserver)target;
            if (GUILayout.Button("Start occlusion"))
                script.StartOcclusion();
        }
    }
}
