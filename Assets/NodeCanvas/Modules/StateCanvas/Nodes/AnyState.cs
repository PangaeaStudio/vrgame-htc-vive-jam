using NodeCanvas.Framework;
using ParadoxNotion.Design;


namespace NodeCanvas.StateMachines{

	[Name("Any State")]
	[Description("The Transitions of this node will constantly be checked. If any becomes true, the target connected State will Enter regardless of the current State. This node can have no incomming transitions.")]
	public class AnyState : FSMState, IUpdatable{

		public override string name{
			get {return string.Format("<color=#b3ff7f>{0}</color>", base.name.ToUpper());}
		}

		public override int maxInConnections{ get {return 0;} }
		public override int maxOutConnections{ get{return -1;} }
		public override bool allowAsPrime{ get {return false;} }

		new public void Update(){

			if (outConnections.Count == 0)
				return;

			status = Status.Running;

			for (var i = 0; i < outConnections.Count; i++){

				var connection = (FSMConnection)outConnections[i];
				var condition = connection.condition;

				if (!connection.isActive || connection.condition == null)
					continue;

				if (condition.CheckCondition(graphAgent, graphBlackboard)){
					FSM.EnterState( (FSMState)connection.targetNode );
					connection.connectionStatus = Status.Success;
					return;
				}

				connection.connectionStatus = Status.Failure;
			}
		}

		////////////////////////////////////////
		///////////GUI AND EDITOR STUFF/////////
		////////////////////////////////////////
		#if UNITY_EDITOR

		protected override void OnNodeInspectorGUI(){

			ShowBaseFSMInspectorGUI();
			if (outConnections.Find(c => (c as FSMConnection).condition == null ) != null)
				UnityEditor.EditorGUILayout.HelpBox("This is not a state and as such it never finish and no OnFinish transitions are ever called.\nAdd a condition in all transitions of this node", UnityEditor.MessageType.Warning);
		}

		#endif
	}
}