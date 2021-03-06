﻿
using Adic;
using Scripts.Common.Data;
using Scripts.Common.Factories;
using Scripts.Core.StateMachine;
using Scripts.Scenes.Lobby.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scripts.Scenes.Lobby.States
{
    public class RatingState : BaseStateMachineState
    {
        [Inject] private ILobbyContentFactory contentFactory;

        [SerializeField] private RatingStageScoreView stageScorePrefab;
        [SerializeField] private RatingPrizeInfoView prizeViewPrefab;
        [SerializeField] private RectTransform progressLineBack;
        [SerializeField] private RectTransform progressLine;
        [SerializeField] private TextMeshProUGUI curRatingUser;
        [SerializeField] private Transform ratingScalaRoot;
        [SerializeField] private ScrollRect ratingScrollRect;

        private GameObject skinPerson = null;
        private List<GameObject> clearList = new List<GameObject>();

        public override void OnStartState( IStateMachine stateMachine, params object[] args )
        {
            base.OnStartState( stateMachine, args );
            this.Inject();

            skinPerson = contentFactory.GetPlayerPerson();
            skinPerson.SetActive( false );

            ClearPreviousProgress();
            CreateProgress();
        }

        public override void OnEndState()
        {
            base.OnEndState();

            skinPerson.SetActive( true );
        }

        public void OnPressedBackToMainmenu()
        {
            stateMachine.SetState( ( int ) LobbyState.Main );
        }

        public void OnPressedGoToSettings()
        {
            stateMachine.SetState( ( int ) LobbyState.Settings );
        }

        private void CreateProgress()
        {
            // generate fake data // FIT IT TODO REMOVE FROM THIS
            int userRating = UnityEngine.Random.Range( 0, 2000 );
            RatingStageData[] ratingStageDatas = new RatingStageData[ 10 ];
            for ( int i = 0; i < ratingStageDatas.Length; i++ )
            {
                ratingStageDatas[ i ] = new RatingStageData( i );
            }

            // show view all about data above
            for ( int i = 0; i < ratingStageDatas.Length; i++ )
            {
                var stageData = ratingStageDatas[ i ];
                float xPos = ( i + 1 ) * 300f;

                var newStageScore = Instantiate<RatingStageScoreView>( stageScorePrefab, stageScorePrefab.transform.parent );
                newStageScore.Setup( stageData );
                newStageScore.gameObject.SetActive( true );
                newStageScore.transform.localPosition = new Vector3( xPos, 0, 0 );

                clearList.Add( newStageScore.gameObject );

                var newPrize = Instantiate<RatingPrizeInfoView>( prizeViewPrefab, prizeViewPrefab.transform.parent );
                newPrize.Setup( stageData );
                newPrize.gameObject.SetActive( true );
                Vector3 pos = prizeViewPrefab.transform.localPosition;
                pos.x = xPos;
                newPrize.transform.localPosition = pos;

                clearList.Add( newPrize.gameObject );
            }

            float lineLength = ratingStageDatas.Length * 300;
            float xK = lineLength / ( float ) ratingStageDatas[  ratingStageDatas.Length - 1 ].Score;
            Vector2 progressBackLineSize = progressLineBack.sizeDelta;
            progressBackLineSize.x = lineLength + 300 * 2;
            progressLineBack.sizeDelta = progressBackLineSize;

            Vector2 progressLineSize = progressLine.sizeDelta;
            progressLineSize.x = userRating * xK;
            progressLine.sizeDelta = progressLineSize;

            curRatingUser.text = userRating.ToString();

            ratingScalaRoot.GetComponent<RectTransform>().sizeDelta = new Vector2( lineLength, 100 );

            float ratingProgressK = userRating * xK / lineLength;
            ratingScrollRect.horizontalNormalizedPosition = ratingProgressK;
        }

        private void ClearPreviousProgress()
        {
            foreach ( var obj in clearList )
            {
                GameObject.Destroy( obj );
            }

            clearList.Clear();
        }
    }
}
