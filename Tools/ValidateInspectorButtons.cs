using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Button=UnityEngine.UI.Button;
public static class ValidateInspectorButtons
{
 static StringBuilder output;
 static int buttons,callbacks,errors;
 static string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
 static void Check(GameObject root)
 {
  foreach(var b in root.GetComponentsInChildren<Button>(true)) {
   buttons++; int actions=0;
   var so=new SerializedObject(b); var calls=so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
   for(int i=0;i<calls.arraySize;i++) {
    var p=calls.GetArrayElementAtIndex(i); var target=p.FindPropertyRelative("m_Target").objectReferenceValue;
    var method=p.FindPropertyRelative("m_MethodName").stringValue; int mode=p.FindPropertyRelative("m_Mode").enumValueIndex;
    var args=mode==3?new[]{typeof(int)}:mode==6?new[]{typeof(bool)}:mode==4?new[]{typeof(float)}:mode==5?new[]{typeof(string)}:Type.EmptyTypes;
    if(target==null || target.GetType().GetMethod(method,args)==null) { errors++; output.AppendLine("INVALID "+Path(b.transform)+" -> "+target+"."+method); }
    else { callbacks++; if(!(target is UIButtonClickSound)) actions++; }
   }
   if(actions==0) output.AppendLine("NO ACTION "+Path(b.transform));
  }
 }
 public static string Main()
 {
  output=new StringBuilder(); buttons=callbacks=errors=0;
  var setup=EditorSceneManager.GetSceneManagerSetup();
  foreach(var path in new[]{"Assets/Scenes/MainScene.unity","Assets/Scenes/GameScene.unity"}) {
   var scene=SceneManager.GetSceneByPath(path); if(!scene.isLoaded) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   foreach(var root in scene.GetRootGameObjects()) Check(root);
  }
  foreach(var path in new[]{"Assets/Prefabs/RoomListItem.prefab","Assets/Prefabs/UI/PlayerSlot.prefab","Assets/Prefabs/UI/SettingsPopup.prefab"}) {
   var root=PrefabUtility.LoadPrefabContents(path); try { Check(root); } finally { PrefabUtility.UnloadPrefabContents(root); }
  }
  EditorSceneManager.RestoreSceneManagerSetup(setup);
  return "Buttons="+buttons+" valid callbacks="+callbacks+" invalid="+errors+"\n"+output;
 }
}
