﻿using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NodeCanvas.Framework;
using NodeCanvas.Framework.Internal;
using ParadoxNotion;
using ParadoxNotion.Design;
using ParadoxNotion.Serialization;
using UnityEngine;


namespace NodeCanvas.Tasks.Actions{

	[Name("Implemented Action (mp)")]
	[Category("✫ Script Control/Multiplatform")]
	[Description("Calls a function that has signature of 'public Status NAME()' or 'public Status NAME(T)'. You should return Status.Success, Failure or Running within that function.")]
	public class ImplementedAction_Multiplatform : ActionTask {

		[SerializeField]
		private SerializedMethodInfo method;
		[SerializeField]
		private List<BBObjectParameter> parameters = new List<BBObjectParameter>();

		private Status actionStatus = Status.Resting;

		private MethodInfo targetMethod{
			get {return method != null && method.Get() != null? method.Get() : null;}
		}

		public override System.Type agentType{
			get {return targetMethod != null? targetMethod.RTReflectedType() : typeof(Transform);}
		}

		protected override string info{
			get
			{
				if (method == null)
					return "No Action Selected";
				if (targetMethod == null)
					return string.Format("<color=#ff6457>* {0} *</color>", method.GetMethodString() );
				return string.Format("[ {0}.{1}({2}) ]", agentInfo, targetMethod.Name, parameters.Count == 1? parameters[0].ToString() : "" );
			}
		}


		protected override string OnInit(){
			if (targetMethod == null)
				return "ImplementedAction Error";
			return null;
		}

		protected override void OnExecute(){ Forward(); }
		protected override void OnUpdate(){	Forward(); }

		void Forward(){

			var args = parameters.Select(p => p.value).ToArray();
			actionStatus = (Status)targetMethod.Invoke(agent, args);

			if (actionStatus == Status.Success){
				EndAction(true);
				return;
			}

			if (actionStatus == Status.Failure){
				EndAction(false);
				return;
			}
		}

		protected override void OnStop(){
			actionStatus = Status.Resting;
		}

		////////////////////////////////////////
		///////////GUI AND EDITOR STUFF/////////
		////////////////////////////////////////
		#if UNITY_EDITOR

		protected override void OnTaskInspectorGUI(){

			if (!Application.isPlaying && GUILayout.Button("Select Action Method")){

				System.Action<MethodInfo> MethodSelected = (method)=>{
					this.method = new SerializedMethodInfo(method);
					this.parameters.Clear();
					foreach(var p in method.GetParameters()){
						var newParam = new BBObjectParameter{bb = blackboard};
						newParam.SetType(p.ParameterType);
						parameters.Add(newParam);
					}					
				};

				if (agent != null){
					EditorUtils.ShowGameObjectMethodSelectionMenu(agent.gameObject, typeof(Status), typeof(object), MethodSelected, 1, false, true);
				} else {
					var menu = new UnityEditor.GenericMenu();
					foreach (var t in UserTypePrefs.GetPreferedTypesList(typeof(Component), true))
						menu = EditorUtils.GetMethodSelectionMenu(t, typeof(Status), typeof(object), MethodSelected, 1, false, true, menu);
					menu.ShowAsContext();
					Event.current.Use();
				}
			}

			if (targetMethod != null){
				GUILayout.BeginVertical("box");
				UnityEditor.EditorGUILayout.LabelField("Type", agentType.FriendlyName());
				UnityEditor.EditorGUILayout.LabelField("Selected Action Method:", targetMethod.Name);
				GUILayout.EndVertical();
				
				if (targetMethod.GetParameters().Length == 1){
					var paramName = targetMethod.GetParameters()[0].Name.SplitCamelCase();
					EditorUtils.BBParameterField(paramName, parameters[0]);
				}
			}
		}
		
		#endif
	}
}