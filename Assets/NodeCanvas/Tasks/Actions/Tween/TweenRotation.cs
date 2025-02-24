﻿using DG.Tweening;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;


namespace NodeCanvas.Tasks.Actions {

    [Category("Tween")]
    [Icon("DOTTween", true)]
    public class TweenRotation : ActionTask<Transform> {

        public BBParameter<Vector3> vector;
        public BBParameter<float> delay = 0f;
        public BBParameter<float> duration = 0.5f;
        public Ease easeType = Ease.Linear;
        public bool relative = false;
        public bool waitActionFinish = true;


        protected override void OnExecute() {

            if ( !relative && agent.eulerAngles == vector.value ){
                EndAction();
                return;
            }

            var tween = agent.DORotate(vector.value, duration.value);
            tween.SetDelay(delay.value);
            tween.SetEase(easeType);
            if (relative)
                tween.SetRelative();

            if (!waitActionFinish) EndAction();
        }

        protected override void OnUpdate() {
            if (elapsedTime >= duration.value + delay.value)
                EndAction();
        }
    }
}