﻿#if UNITY_EDITOR

using ParadoxNotion.Design;
using NodeCanvas.Framework;
using UnityEditor;
using UnityEngine;

namespace NodeCanvas.Editor{

	public class ExternalInspectorWindow : EditorWindow {

		private object currentSelection;
		private Vector2 scrollPos;

		void OnEnable(){
	        #if UNITY_5_1
	        titleContent = new GUIContent("NC Inspector");
	        #else
	        title = "NC Inspector";
	        #endif
	        
	        NCPrefs.useExternalInspector = true;
		}

		void OnDestroy(){
			NCPrefs.useExternalInspector = false;
		}

		void Update(){
			if (currentSelection != Graph.currentSelection)
				Repaint();
		}

		void OnGUI(){

			if (GraphEditor.currentGraph == null){
				GUILayout.Label("No current NodeCanvas Graph open");				
				return;
			}
				
			if (EditorApplication.isCompiling){
				ShowNotification(new GUIContent("Compiling Please Wait..."));
				return;			
			}

			currentSelection = Graph.currentSelection;

			if (currentSelection == null){
				GUILayout.Label("No Node Selected in Canvas");
				return;
			}

			scrollPos = GUILayout.BeginScrollView(scrollPos);

			if (currentSelection is Node){
				var node = (Node)currentSelection;
				Title(node.name);
				node.ShowNodeInspectorGUI();
			}
			
			if (currentSelection is Connection){
				Title("Connection");
				(currentSelection as Connection).ShowConnectionInspectorGUI();
			}

			GUILayout.EndScrollView();

			EditorUtils.EndOfInspector();
		}

		void Title(string text){

			GUILayout.Space(5);
			GUILayout.BeginHorizontal("box", GUILayout.Height(28));
			GUILayout.FlexibleSpace();
			GUILayout.Label("<b><size=16>" + text + "</size></b>");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			EditorUtils.BoldSeparator();
		}

	    [MenuItem("Window/NodeCanvas/External Inspector")]
	    public static void OpenWindow() {

	        var window = GetWindow(typeof(ExternalInspectorWindow)) as ExternalInspectorWindow;
	        window.Show();
	    }
	}
}

#endif