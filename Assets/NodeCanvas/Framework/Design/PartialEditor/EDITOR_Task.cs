﻿#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEditor;
using UnityEngine;


namespace NodeCanvas.Framework{

	partial class Task{

		private bool _isUnfolded = true;
		private Texture2D _icon;

		//The icon if any of the task
		public Texture2D icon{
			get
			{
				if (_icon == null){
					var iconAtt = this.GetType().RTGetAttribute<IconAttribute>(true);
					if (iconAtt != null) _icon = (Texture2D)Resources.Load(iconAtt.iconName);
				}
				return _icon;
			}
		}

		private bool isUnfolded{
			get {return _isUnfolded;}
			set {_isUnfolded = value;}
		}

		//A placeholder for a copy/paste operation
		public static Task copiedTask{get;set;}

		//Draw the task inspector GUI
		public static void ShowTaskInspectorGUI(Task task, Action<Task> callback, bool showTitlebar = true){

			if (task.ownerSystem == null){
				GUILayout.Label("<b>Owner System is null! This should have not happen! Please report a bug</b>");
				return;
			}

			//make sure TaskAgent is not null in case task defines an AgentType
			if (task.agentIsOverride && task.overrideAgent == null){
				task.overrideAgent = new TaskAgent();
			}

			UndoManager.CheckUndo(task.ownerSystem.baseObject, "Task Inspector");

			if (task.isObsolete != null)
				EditorGUILayout.HelpBox(string.Format("This is an obsolete Task:\n\"{0}\"", task.isObsolete.Message), MessageType.Warning);

			if (!showTitlebar || ShowTaskTitlebar(task, callback) == true){

				if (!string.IsNullOrEmpty(task.description))
					EditorGUILayout.HelpBox(task.description, MessageType.None);

				SpecialCaseInspector(task);
				ShowAgentField(task);
				task.OnTaskInspectorGUI();
			}

			UndoManager.CheckDirty(task.ownerSystem.baseObject);
		}

		//Some special cases for Action & Condition. Bit weird but better that creating a virtual method in this case
		static void SpecialCaseInspector(Task task){
			
			if (task is ActionTask){
				if (Application.isPlaying){
					if ( (task as ActionTask).elapsedTime > 0) GUI.color = Color.yellow;
					EditorGUILayout.LabelField("Elapsed Time", (task as ActionTask).elapsedTime.ToString());
					GUI.color = Color.white;
				}
			}

			if (task is ConditionTask){
				GUI.color = (task as ConditionTask).invert? Color.white : new Color(1f,1f,1f,0.5f);
				(task as ConditionTask).invert = EditorGUILayout.ToggleLeft("Invert Condition", (task as ConditionTask).invert);
				GUI.color = Color.white;
			}
		}

		///Optional override to show custom controls whenever the ShowTaskInspectorGUI is called. By default controls will automaticaly show for most types
		virtual protected void OnTaskInspectorGUI(){ DrawDefaultInspector(); }
		///Draw an automatic editor inspector for this task.
		protected void DrawDefaultInspector(){ EditorUtils.ShowAutoEditorGUI(this); }


		//a Custom titlebar for tasks
		static bool ShowTaskTitlebar(Task task, Action<Task> callback){

			GUI.backgroundColor = new Color(1,1,1,0.8f);
			GUILayout.BeginHorizontal("box");
			GUI.backgroundColor = Color.white;

			if (GUILayout.Button("X", GUILayout.Width(20))){
				if (callback != null)
					callback(null);
				return false;
			}

			GUILayout.Label("<b>" + (task.isUnfolded? "▼ " :"► ") + task.name + "</b>" + (task.isUnfolded? "" : "\n<i><size=10>(" + task.summaryInfo + ")</size></i>") );

			if (GUILayout.Button(EditorUtils.csIcon, (GUIStyle)"label", GUILayout.Width(20), GUILayout.Height(20))){
				EditorUtils.OpenScriptOfType(task.GetType());
			}

			GUILayout.EndHorizontal();
			var titleRect = GUILayoutUtility.GetLastRect();
			EditorGUIUtility.AddCursorRect(titleRect, MouseCursor.Link);

			var e = Event.current;

			if (e.type == EventType.ContextClick && titleRect.Contains(e.mousePosition)){
				var menu = new GenericMenu();
				menu.AddItem(new GUIContent("Open Script"), false, ()=> { EditorUtils.OpenScriptOfType(task.GetType()); } );
				menu.AddItem(new GUIContent("Copy"), false, ()=> {Task.copiedTask = task;} );
				menu.AddItem(new GUIContent("Delete"), false, ()=>
				{
					if (callback != null)
						callback(null);
				});


				if (task is ActionList){
					menu.AddItem(new GUIContent("Save Preset"), false, ()=> { (task as ActionList).DoSavePreset(); } );
					menu.AddItem(new GUIContent("Load Preset"), false, ()=> { (task as ActionList).DoLoadPreset(); } );
				}

				if (task is ConditionList){
					menu.AddItem(new GUIContent("Save Preset"), false, ()=> { (task as ConditionList).DoSavePreset(); } );
					menu.AddItem(new GUIContent("Load Preset"), false, ()=> { (task as ConditionList).DoLoadPreset(); } );
				}

				
				menu.ShowAsContext();
				e.Use();
			}

			if (e.button == 0 && e.type == EventType.MouseDown && titleRect.Contains(e.mousePosition))
				e.Use();

			if (e.button == 0 && e.type == EventType.MouseUp && titleRect.Contains(e.mousePosition)){
				task.isUnfolded = !task.isUnfolded;
				e.Use();
			}

			return task.isUnfolded;
		}


		//Shows the agent field in case an agent type is specified either with [AgentType] attribute or through the use of the generic versions of Actio or Condition Task
		static void ShowAgentField(Task task){

			if (task.agentType == null)
				return;

			if (Application.isPlaying && task.agentIsOverride && task.overrideAgent.value == null){
				GUI.color = EditorUtils.lightRed;
				EditorGUILayout.LabelField("Missing Agent Reference!");
				GUI.color = Color.white;
				return;
			}

			var isMissingType = task.agent == null || task.agent.GetComponent(task.agentType) == null;
			var infoString = isMissingType? "<color=#ff5f5f>" + task.agentType.FriendlyName() + "</color>": task.agentType.FriendlyName();

			GUI.color = new Color(1f,1f,1f, task.agentIsOverride? 1f : 0.5f);
			GUI.backgroundColor = GUI.color;
			GUILayout.BeginVertical(task.agentIsOverride? "box" : "button");
			GUILayout.BeginHorizontal();


			if (task.agentIsOverride){

				if (task.overrideAgent.useBlackboard){

					GUI.color = new Color(0.9f,0.9f,1f,1f);
					var dataNames = new List<string>();
					
					//Local
					if (task.overrideAgent.bb != null){
						dataNames.AddRange(task.overrideAgent.bb.GetVariableNames(task.agentType));
						dataNames.AddRange(task.overrideAgent.bb.GetVariableNames(typeof(GameObject)));
					}

					//Globals
					foreach (var globalBB in GlobalBlackboard.allGlobals){
						if (globalBB == task.overrideAgent.bb)
							continue;
						dataNames.Add(globalBB.name + "/");
						var globalVars = globalBB.GetVariableNames(task.agentType);
						for (var i = 0; i < globalVars.Length; i++)
							globalVars[i] = globalBB.name + "/" + globalVars[i];
						dataNames.AddRange( globalVars );
					}

					//No Dynamic for AgentField for convenience and user error safety
					//

					if (dataNames.Contains(task.overrideAgent.name) || string.IsNullOrEmpty(task.overrideAgent.name) ){
						task.overrideAgent.name = EditorUtils.StringPopup(task.overrideAgent.name, dataNames, false, true);
					} else {
						task.overrideAgent.name = EditorGUILayout.TextField(task.overrideAgent.name);
					}

				} else {

					GUI.enabled = !EditorUtility.IsPersistent(task.ownerSystem.baseObject);
					task.overrideAgent.value = EditorGUILayout.ObjectField(task.overrideAgent.value, task.agentType, true) as Component;
					GUI.enabled = true;
				}

			} else {

				GUILayout.BeginHorizontal();
				var compIcon = EditorGUIUtility.ObjectContent(null, task.agentType).image;
				if (compIcon != null)
					GUILayout.Label(compIcon, GUILayout.Width(16), GUILayout.Height(16));
				GUILayout.Label(string.Format("Use Self ({0})", infoString));
				GUILayout.EndHorizontal();				
			}


			GUI.color = Color.white;

			if (task.agentIsOverride){
				if (isMissingType)
					GUILayout.Label("(" + infoString + ")", GUILayout.Height(15));
				task.overrideAgent.useBlackboard = EditorGUILayout.Toggle(task.overrideAgent.useBlackboard, EditorStyles.radioButton, GUILayout.Width(18));
			}

			GUI.color = Color.white;
			GUI.backgroundColor = Color.white;

			if (!Application.isPlaying)
				task.agentIsOverride = EditorGUILayout.Toggle(task.agentIsOverride, GUILayout.Width(18));

			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}
	}
}

#endif
