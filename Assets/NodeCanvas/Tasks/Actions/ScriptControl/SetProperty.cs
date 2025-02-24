﻿using System.Reflection;
using NodeCanvas.Framework;
using NodeCanvas.Framework.Internal;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEngine;


namespace NodeCanvas.Tasks.Actions{

	[Category("✫ Script Control/Standalone Only")]
	[Description("Set a property on a script")]
	public class SetProperty : ActionTask {

		[SerializeField] [IncludeParseVariables]
		private ReflectedActionWrapper functionWrapper;

		private MethodInfo targetMethod{
			get {return functionWrapper != null && functionWrapper.GetMethod() != null? functionWrapper.GetMethod() : null;}
		}

		public override System.Type agentType{
			get {return targetMethod != null? targetMethod.RTReflectedType() : typeof(Transform);}
		}

		protected override string info{
			get
			{
				if (functionWrapper == null)
					return "No Property Selected";
				if (targetMethod == null)
					return string.Format("<color=#ff6457>* {0} *</color>", functionWrapper.GetMethodString() );
				return string.Format("{0}.{1} = {2}", agentInfo, targetMethod.Name, functionWrapper.GetVariables()[0] );
			}
		}

		//store the method info on init for performance
		protected override string OnInit(){
			if (targetMethod == null)
				return "SetProperty Error";
			try
			{
				functionWrapper.Init(agent);
				return null;
			}
			catch {return "SetProperty Error";}
		}

		//do it by invoking method
		protected override void OnExecute(){

			if (functionWrapper == null){
				EndAction(false);
				return;
			}

			functionWrapper.Call();
			EndAction();
		}

		////////////////////////////////////////
		///////////GUI AND EDITOR STUFF/////////
		////////////////////////////////////////
		#if UNITY_EDITOR

		protected override void OnTaskInspectorGUI(){

			if (!Application.isPlaying && GUILayout.Button("Select Property")){

				System.Action<MethodInfo> MethodSelected = (method)=> {
					functionWrapper = ReflectedActionWrapper.Create(method, blackboard);
				};

				if (agent != null){
					EditorUtils.ShowGameObjectMethodSelectionMenu(agent.gameObject, typeof(void), typeof(object), MethodSelected, 1, true, false);
				} else {
					var menu = new UnityEditor.GenericMenu();
					foreach (var t in UserTypePrefs.GetPreferedTypesList(typeof(Component), true))
						menu = EditorUtils.GetMethodSelectionMenu(t, typeof(void), typeof(object), MethodSelected, 1, true, false, menu);
					menu.ShowAsContext();
					Event.current.Use();
				}				
			}

			if (targetMethod != null){
				GUILayout.BeginVertical("box");
				UnityEditor.EditorGUILayout.LabelField("Type", agentType.FriendlyName());
				UnityEditor.EditorGUILayout.LabelField("Property", targetMethod.Name);
				UnityEditor.EditorGUILayout.LabelField("Set Type", functionWrapper.GetVariables()[0].varType.FriendlyName() );
				GUILayout.EndVertical();
				EditorUtils.BBParameterField("Set Value", functionWrapper.GetVariables()[0]);
			}
		}

		#endif
	}
}