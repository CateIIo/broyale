﻿
using System;
using RemoteConfig;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Scripts.Common.UI
{
    public class AbilityDraggingEventArgs : EventArgs
    {
        public SkillInfo SkillInfo { get; private set; }

        public AbilityDraggingEventArgs( SkillInfo skillInfo )
        {
            this.SkillInfo = skillInfo;
        }
    }

    public class AbilityDraggingItem : AbilitySelectedItem
    {
        [SerializeField] private GameObject[] progressLines;

        private SkillInfo skillInfo;

        public SkillInfo GetMySkillInfo() { return skillInfo; }

        public void Setup( SkillInfo skillInfo, Sprite sprite )
        {
            this.skillInfo = skillInfo;

            base.Setup( sprite );

            bool updgradeMode = UnityEngine.Random.Range( 0, 1000 ) > 500; // ERESH TODO FIX IT
            SetupInside( updgradeMode );
        }

        private void SetupInside( bool upgradeMode )
        {
            progressLines[ 0 ].SetActive( ! upgradeMode );
            progressLines[ 1 ].SetActive( upgradeMode );
        }

        public override void OnPointerClick( PointerEventData evData )
        {
            if ( listener != null )
            {
                AbilityDraggingEventArgs args = new AbilityDraggingEventArgs( skillInfo );
                listener.OnEvent( this, EVENT_TAP_LIGHTED_ABILITY, args );
            }
        }
    }
}
