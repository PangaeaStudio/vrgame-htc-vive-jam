﻿using System;
using System.Collections;
using ParadoxNotion.Services;
using UnityEngine;


namespace NodeCanvas.Framework{

    ///Base class for all actions. Extend this to create your own. T is the agentType required by the action.
    ///Generic version to define the AgentType instead of using the [AgentType] attribute
	abstract public class ActionTask<T> : ActionTask where T:Component{

		public override Type agentType{
			get {return typeof(T);}
		}

		new public T agent{
			get
			{
				try { return (T)base.agent; }
				catch {return null;}
			}
		}		
	}

	///Base class for all actions. Extend this to create your own.
	abstract public class ActionTask : Task{

		[NonSerialized]
		private Status status = Status.Resting;
		[NonSerialized]
		private float startedTime;
		[NonSerialized]
		private float pausedTime;
		[NonSerialized]
		private bool latch = false;
		[NonSerialized]
		private bool _isPaused;

		///The time in seconds this action is running if at all
		public float elapsedTime{
			get
			{
				if (isPaused)
					return pausedTime - startedTime;
				if (isRunning)
					return Time.time - startedTime;
				return 0;
			}
		}

		///Is the action currently running?
		public bool isRunning{
			get {return status == Status.Running;}
		}

		///Is the action currently paused?
		public bool isPaused{
			get {return _isPaused;}
			private set {_isPaused = value;}
		}


		////////
		////////

		///Used to call an action providing a callback. Not used in NC Graphs
		public void ExecuteAction(Component agent, Action<bool> callback){
			ExecuteAction(agent, null, callback);
		}

		///Used to call an action providing a callback. Not used in NC Graphs
		public void ExecuteAction(Component agent, IBlackboard blackboard, Action<bool> callback){
			if (!isRunning)
				MonoManager.current.StartCoroutine(ActionUpdater(agent, blackboard, callback));			
		}

		//The internal updater for when an action has been called with a callback parameter
		IEnumerator ActionUpdater(Component agent, IBlackboard blackboard, Action<bool> callback){
			while(ExecuteAction(agent, blackboard) == Status.Running)
				yield return null;
			if (callback != null)
				callback(status == Status.Success? true : false);
		}

		///Executes the action for the provided agent and blackboard
		public Status ExecuteAction(Component agent, IBlackboard blackboard){

			if (!isActive)
				return Status.Failure;

			if (isPaused){ //is resume
				startedTime += Time.time - pausedTime;
				isPaused = false;
			}

			if (status == Status.Running){
				OnUpdate();
				latch = false;
				return status;
			}

			if (latch){ //to be possible to call EndAction anywhere
				latch = false;
				return status;
			}

			if (!Set(agent, blackboard)){
				isActive = false;
				return Status.Failure;
			}

			startedTime = Time.time;
			status = Status.Running;
			OnExecute();
			latch = false;
			return status;
		}

		///Ends the action either in success or failure. Ending with null means that it's a cancel/interrupt. Used by the external system.
		public void EndAction(){ EndAction(true); }
		public void EndAction(bool? success){

			if (status != Status.Running)
				return;
			
			isPaused = false;
			status = success == true? Status.Success : Status.Failure;
			latch = success != null? true : false;
			OnStop();
		}

		///Pause the action from updating and calls OnPause
		public void PauseAction(){
			
			if (status != Status.Running)
				return;

			pausedTime = Time.time;
			isPaused = true;
			OnPause();
		}

		///Override in your own actions. Called once when the actions is executed.
		virtual protected void OnExecute(){}

		///Override in your own actions. Called every frame, if and while the action is running and until it ends.
		virtual protected void OnUpdate(){}
		
		///Called whenever the action ends due to any reason.
		virtual protected void OnStop(){}

		///Called when the action is paused
		virtual protected void OnPause(){}
	}
}