using UnityEditor;

[CustomEditor(typeof(BizzaAnalyticsAgent))]
public sealed class BizzaAnalyticsAgentEditor : Editor
{
  public override void OnInspectorGUI()
  {
    serializedObject.Update();
    using (new EditorGUI.DisabledScope(true))
    {
      EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
    }
    DrawPropertiesExcluding(serializedObject, "m_Script", "reportingPercentage");
    serializedObject.ApplyModifiedProperties();
  }
}
