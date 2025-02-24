﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NodeCanvas.Framework;
using NodeCanvas.Framework.Internal;
using ParadoxNotion;
using ParadoxNotion.Design;
using ParadoxNotion.Serialization;
using UnityEngine;


namespace NodeCanvas.Tasks.Conditions{

	[Name("Check Function (mp)")]
	[Category("✫ Script Control/Multiplatform")]
	[Description("Call a function with none or up to 3 parameters on a component and return whether or not the return value is equal to the check value")]
	public class CheckFunction_Multiplatform : ConditionTask {

		[SerializeField]
		private SerializedMethodInfo method;
		[SerializeField]
		private List<BBObjectParameter> parameters = new List<BBObjectParameter>();
		[SerializeField] [BlackboardOnly]
		private BBObjectParameter checkValue;
		[SerializeField]
		private CompareMethod comparison;

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
					return "No Method Selected";
				if (targetMethod == null)
					return string.Format("<color=#ff6457>* {0} *</color>", method.GetMethodString() );

				var paramInfo = "";
				for (var i = 0; i < parameters.Count; i++)
					paramInfo += (i != 0? ", " : "") + parameters[i].ToString();
				return string.Format("{0}.{1}({2}){3}", agentInfo, targetMethod.Name, paramInfo, OperationTools.GetCompareString(comparison) + checkValue);
			}
		}

		//store the method info on agent set for performance
		protected override string OnInit(){
			if (targetMethod == null)
				return "CheckFunction Error";
			return null;
		}

		//do it by invoking method
		protected override bool OnCheck(){
			
			var args = parameters.Select(p => p.value).ToArray();

			if (checkValue.varType == typeof(float))
				return OperationTools.Compare( (float)targetMethod.Invoke(agent, args), (float)checkValue.value, comparison, 0.05f );
			if (checkValue.varType == typeof(int))
				return OperationTools.Compare( (int)targetMethod.Invoke(agent, args), (int)checkValue.value, comparison );
			return object.Equals(targetMethod.Invoke(agent, args), checkValue.value);
		}

		////////////////////////////////////////
		///////////GUI AND EDITOR STUFF/////////
		////////////////////////////////////////
		#if UNITY_EDITOR

		protected override void OnTaskInspectorGUI(){

			if (!Application.isPlaying && GUILayout.Button("Select Method")){
				
				System.Action<MethodInfo> MethodSelected = (method)=> {
					this.method = new SerializedMethodInfo(method);
					this.parameters.Clear();
					foreach(var p in method.GetParameters()){
						var newParam = new BBObjectParameter{bb = blackboard};
						newParam.SetType(p.ParameterType);
						if (p.IsOptional){
							newParam.value = p.DefaultValue;
						}
						parameters.Add(newParam);
					}

					this.checkValue = new BBObjectParameter{bb = blackboard};
					this.checkValue.SetType(method.ReturnType);
					comparison = CompareMethod.EqualTo;
				};

				if (agent != null){
					EditorUtils.ShowGameObjectMethodSelectionMenu(agent.gameObject, typeof(object), typeof(object), MethodSelected, 3, false, true);
				} else {
					var menu = new UnityEditor.GenericMenu();
					foreach (var t in UserTypePrefs.GetPreferedTypesList(typeof(Component), true))
						menu = EditorUtils.GetMethodSelectionMenu(t, typeof(object), typeof(object), MethodSelected, 3, false, true, menu);
					menu.ShowAsContext();
					Event.current.Use();
				}
			}

			if (targetMethod != null){
				GUILayout.BeginVertical("box");
				UnityEditor.EditorGUILayout.LabelField("Type", agentType.FriendlyName());
				UnityEditor.EditorGUILayout.LabelField("Method", targetMethod.Name);
				GUILayout.EndVertical();

				var paramNames = targetMethod.GetParameters().Select(p => p.Name.SplitCamelCase() ).ToArray();
				for (var i = 0; i < paramNames.Length; i++){
					EditorUtils.BBParameterField(paramNames[i], parameters[i]);
				}

				GUI.enabled = checkValue.varType == typeof(float) || checkValue.varType == typeof(int);
				comparison = (CompareMethod)UnityEditor.EditorGUILayout.EnumPopup("Comparison", comparison);
				GUI.enabled = true;				
				EditorUtils.BBParameterField("Check Value", checkValue);
			}
		}

		#endif
	}
}