﻿#if UNITY_EDITOR

using System.Collections;
using NodeCanvas.Editor;
using ParadoxNotion.Design;
using ParadoxNotion.Services;
using UnityEditor;
using UnityEngine;


namespace NodeCanvas.Framework{

	partial class Connection{

		protected enum TipConnectionStyle
		{
			None,
			Circle,
			Arrow
		}

		[SerializeField]
		private bool _infoExpanded      = true;
		
		private Rect areaRect           = new Rect(0,0,50,10);
		private Status lastStatus       = Status.Resting;
		private Color connectionColor   = Node.restingColor;
		private float lineSize          = 3;
		private bool nowSwitchingColors = false;
		private Vector3 lineFromTangent = Vector3.zero;
		private Vector3 lineToTangent   = Vector3.zero;
		private bool isRelinking        = false;
		private Rect outPortRect;

		readonly private static float defaultLineSize = 3;

		private bool infoExpanded{
			get {return _infoExpanded;}
			set {_infoExpanded = value;}
		}

		virtual protected TipConnectionStyle tipConnectionStyle{
			get {return TipConnectionStyle.Circle;}
		}

		virtual protected bool canRelink{
			get {return true;}
		}

		//Draw connection from-to
		public void DrawConnectionGUI(Vector3 lineFrom, Vector3 lineTo){
			
			//curveMode 0 is smooth
			var mlt = NCPrefs.curveMode == 0? 0.8f : 1f;
			var tangentX = Mathf.Abs(lineFrom.x - lineTo.x) * mlt;
			var tangentY = Mathf.Abs(lineFrom.y - lineTo.y) * mlt;

			GUI.color = connectionColor;
			var arrowRect = new Rect(0,0,15,15);
			arrowRect.center = lineTo;

			var hor = 0;

			if (lineFrom.x <= sourceNode.nodeRect.x){
				lineFromTangent = new Vector3(-tangentX, 0, 0);
				hor--;
			}

			if (lineFrom.x >= sourceNode.nodeRect.xMax){
				lineFromTangent = new Vector3(tangentX, 0, 0);
				hor++;
			}

			if (lineFrom.y <= sourceNode.nodeRect.y)
				lineFromTangent = new Vector3(0, -tangentY, 0);

			if (lineFrom.y >= sourceNode.nodeRect.yMax)
				lineFromTangent = new Vector3(0, tangentY, 0);


			if (lineTo.x <= targetNode.nodeRect.x){
				lineToTangent = new Vector3(-tangentX, 0, 0);
				hor--;
				if (tipConnectionStyle == TipConnectionStyle.Circle)
					GUI.Box(arrowRect, "", "circle");
				else
				if (tipConnectionStyle == TipConnectionStyle.Arrow)
					GUI.Box(arrowRect, "", "arrowRight");
			}

			if (lineTo.x >= targetNode.nodeRect.xMax){
				lineToTangent = new Vector3(tangentX, 0, 0);
				hor++;
				if (tipConnectionStyle == TipConnectionStyle.Circle)
					GUI.Box(arrowRect, "", "circle");
				else
				if (tipConnectionStyle == TipConnectionStyle.Arrow)
					GUI.Box(arrowRect, "", "arrowLeft");
			}

			if (lineTo.y <= targetNode.nodeRect.y){
				lineToTangent = new Vector3(0, -tangentY, 0);
				if (tipConnectionStyle == TipConnectionStyle.Circle)
					GUI.Box(arrowRect, "", "circle");
				else
				if (tipConnectionStyle == TipConnectionStyle.Arrow)
					GUI.Box(arrowRect, "", "arrowBottom");
			}

			if (lineTo.y >= targetNode.nodeRect.yMax){
				lineToTangent = new Vector3(0, tangentY, 0);
				if (tipConnectionStyle == TipConnectionStyle.Circle)
					GUI.Box(arrowRect, "", "circle");
				else
				if (tipConnectionStyle == TipConnectionStyle.Arrow)
					GUI.Box(arrowRect, "", "arrowTop");
			}

			GUI.color = Color.white;

			///

			outPortRect = new Rect(0,0,12,12);
			outPortRect.center = lineFrom;

			if (!Application.isPlaying){
				connectionColor = Node.restingColor;
				lineSize = Graph.currentSelection == this || Graph.currentSelection == sourceNode? 5 : defaultLineSize;
			}

			HandleEvents();




			connectionColor = isActive? connectionColor : new Color(0.3f, 0.3f, 0.3f);

			//check this != null for when in playmode user removes a running connection
			if (Application.isPlaying && this != null && connectionStatus != lastStatus && !nowSwitchingColors && isActive){
				MonoManager.current.StartCoroutine(ChangeLineColorAndSize());
				lastStatus = connectionStatus;
			}

			Handles.color = connectionColor;
			if (NCPrefs.curveMode == 0){
				var shadow = new Vector3(3.5f,3.5f,0);
				Handles.DrawBezier(lineFrom, lineTo+shadow, lineFrom+shadow + lineFromTangent+shadow, lineTo+shadow + lineToTangent, new Color(0,0,0,0.1f), null, lineSize+10f);
				Handles.DrawBezier(lineFrom, lineTo, lineFrom + lineFromTangent, lineTo + lineToTangent, connectionColor, null, lineSize);
			} else {
				var shadow = new Vector3(1,1,0);
				Handles.DrawPolyLine(lineFrom, lineFrom + lineFromTangent * (hor == 0? 0.5f :1), lineTo + lineToTangent* (hor == 0? 0.5f :1), lineTo);
				Handles.DrawPolyLine(lineFrom+shadow, (lineFrom + lineFromTangent * (hor == 0? 0.5f:1))+shadow , (lineTo + lineToTangent* (hor == 0? 0.5f:1))+shadow, lineTo+shadow);
			}

			Handles.color = Color.white;

			//Find the mid position of the connection to draw GUI stuff there
			Vector2 midPosition;

			var t = 0.5f;
			var B1 = t*t*t;
			var B2 = 3*t*t*(1-t);
			var B3 = 3*t*(1-t)*(1-t);
			var B4 = (1-t)*(1-t)*(1-t);
			var C1 = lineFrom;
			var C2 = lineFrom - (lineFromTangent/2);
			var C3 = lineTo - (lineToTangent/2);
			var C4 = lineTo;
			var posX = C1.x * B1 + C2.x * B2 + C3.x * B3 + C4.x * B4;
			var posY = C1.y * B1 + C2.y * B2 + C3.y * B3 + C4.y * B4;

			midPosition = new Vector2(posX, posY);
			midPosition += (Vector2)(lineFromTangent + lineToTangent) /2;
			areaRect.center = midPosition;


			/////Information showing in the middle
			var alpha = (infoExpanded || Graph.currentSelection == this || Graph.currentSelection == sourceNode)? 0.8f : 0.1f;
			var info = GetConnectionInfo(infoExpanded);
			var extraInfo = sourceNode.GetConnectionInfo(sourceNode.outConnections.IndexOf(this) );
			if (!string.IsNullOrEmpty(info) || !string.IsNullOrEmpty(extraInfo)){
				
				if (!string.IsNullOrEmpty(extraInfo) && !string.IsNullOrEmpty(info))
					extraInfo = "\n" + extraInfo;

				var textToShow = string.Format("<size=9>{0}{1}</size>", info, extraInfo);
				if (!infoExpanded)
					textToShow = "<size=9>-||-</size>";
				var finalSize = new GUIStyle("Box").CalcSize(new GUIContent(textToShow));

				areaRect.width = finalSize.x;
				areaRect.height = finalSize.y;

				GUI.color = new Color(1f,1f,1f,alpha);
				GUI.Box(areaRect, textToShow);
				GUI.color = Color.white;

			} else {
			
				areaRect.width = 0;
				areaRect.height = 0;
			}
		}

		//The information to show in the middle area of the connection
		virtual protected string GetConnectionInfo(bool isExpanded){
			return null;
		}

		
		//The connection's inspector
		public void ShowConnectionInspectorGUI(){

			GUILayout.BeginHorizontal();
			GUI.color = new Color(1,1,1,0.5f);

			if (GUILayout.Button("◄", GUILayout.Height(14), GUILayout.Width(20)))
				Graph.currentSelection = sourceNode;

			if (GUILayout.Button("►", GUILayout.Height(14), GUILayout.Width(20)))
				Graph.currentSelection = targetNode;

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("X", GUILayout.Height(14), GUILayout.Width(20))){
				Graph.PostGUI += delegate { graph.RemoveConnection(this); };
				return;
			}

			GUI.color = Color.white;
			GUILayout.EndHorizontal();

			UndoManager.CheckUndo(graph, "Connection Inspector");

			isActive = EditorGUILayout.ToggleLeft("ACTIVE", isActive, GUILayout.Width(150));
			EditorUtils.BoldSeparator();
			OnConnectionInspectorGUI();
			sourceNode.OnConnectionInspectorGUI(sourceNode.outConnections.IndexOf(this));

			UndoManager.CheckDirty(graph);
		}

		//Editor.Override to show controls in the editor panel when connection is selected
		virtual protected void OnConnectionInspectorGUI(){}

		void HandleEvents(){

			var e = Event.current;
			//On click select this connection
			if ( (Graph.allowClick && e.type == EventType.MouseDown && e.button == 0) && (areaRect.Contains(e.mousePosition) || outPortRect.Contains(e.mousePosition) )){
				if (canRelink && !outPortRect.Contains(e.mousePosition)){
					isRelinking = true;
				}
				Graph.currentSelection = this;
				e.Use();
				return;
			}

			if (canRelink && isRelinking){
				Handles.DrawBezier(areaRect.center, e.mousePosition, areaRect.center, e.mousePosition, new Color(1,1,1,0.5f), null, 2);
				if (e.type == EventType.MouseUp){
					foreach(var node in graph.allNodes){
						if (node.nodeRect.Contains(e.mousePosition) && node.IsNewConnectionAllowed(sourceNode) ){
							SetTarget(node);
							break;
						}
					}
					isRelinking = false;
					e.Use();
				} 
			}

			//with delete key, remove connection
			if (Graph.currentSelection == this && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && GUIUtility.keyboardControl == 0){
				Graph.PostGUI += delegate { graph.RemoveConnection(this); };
				e.Use();
				return;
			}

			if (Graph.allowClick && e.type == EventType.MouseDown && e.button == 1 && areaRect.Contains(e.mousePosition)){
				var menu = new GenericMenu();
				menu.AddItem(new GUIContent(infoExpanded? "Collapse Info" : "Expand Info"), false, ()=> { infoExpanded = !infoExpanded; });
				
				var assignable = this as ITaskAssignable;
				if (assignable != null){
					
					if (assignable.task != null){
						menu.AddItem(new GUIContent("Copy Assigned Condition"), false, ()=> { Task.copiedTask = assignable.task; });
					} else {
						menu.AddDisabledItem(new GUIContent("Copy Assigned Condition"));
					}

					if (Task.copiedTask != null){
						menu.AddItem(new GUIContent(string.Format("Pase Assigned Condition ({0})", Task.copiedTask.name)), false, ()=>
						{
							if (assignable.task == Task.copiedTask)
								return;

							if (assignable.task != null){
								if (!EditorUtility.DisplayDialog("Paste Condition", string.Format("Connection already has a Condition assigned '{0}'. Replace assigned condition with pasted condition '{1}'?", assignable.task.name, Task.copiedTask.name), "YES", "NO"))
									return;								
							}

							try {assignable.task = Task.copiedTask.Duplicate(graph);}
							catch {Debug.LogWarning("Can't paste Condition here. Incombatible Types");}
						});

					} else {
						menu.AddDisabledItem(new GUIContent("Paste Assigned Condition"));
					}

				}

				menu.AddSeparator("/");
				menu.AddItem(new GUIContent("Delete"), false, ()=> { graph.RemoveConnection(this); });

				Graph.PostGUI += ()=> { menu.ShowAsContext(); };
				e.Use();
			}

		}

		//Simple tween to enhance the GUI line for debugging.
		IEnumerator ChangeLineColorAndSize(){

			var effectLength = 0.2f;
			float timer = 0;

			//no tween when its going to become resting
			if (connectionStatus == Status.Resting){
				connectionColor = Node.restingColor;
				yield break;
			}

			if (connectionStatus == Status.Success)
				connectionColor = Node.successColor;

			if (connectionStatus == Status.Failure)
				connectionColor = Node.failureColor;

			if (connectionStatus == Status.Running)
				connectionColor = Node.runningColor;

			nowSwitchingColors = true;
				
			while(timer < effectLength){

				timer += Time.deltaTime;
				lineSize = Mathf.Lerp(5, defaultLineSize, timer/effectLength);
				yield return null;
			}

			if (connectionStatus == Status.Resting)
				connectionColor = Node.restingColor;

			if (connectionStatus == Status.Success)
				connectionColor = Node.successColor;

			if (connectionStatus == Status.Failure)
				connectionColor = Node.failureColor;

			if (connectionStatus == Status.Running)
				connectionColor = Node.runningColor;
			
			nowSwitchingColors = false;
		}
	}
}

#endif