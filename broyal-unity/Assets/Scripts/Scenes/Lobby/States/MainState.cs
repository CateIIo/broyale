﻿
using System;
using System.Linq;
using Scripts.Core.StateMachine;
using SocketIO;
using TMPro;
using FullSerializer;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using Scripts.Common.Data.Data;
using Scripts.Common.Data;

namespace Scripts.Scenes.Lobby.States
{
    public class MainState : BaseStateMachineState
    {
        [SerializeField] private TMP_Text connectingTimer;

        private ConnectionStatus status;
        private SocketIOComponent _socket;

        public GameData[] _games;

        private string currentGameName = null;
        private string currentGameId = null;

        public override void OnStartState( IStateMachine stateMachine, params object[] args )
        {
            base.OnStartState( stateMachine, args );

            _socket = GameObject.FindObjectOfType<SocketIOComponent>();

            _socket.On( LobbyEvents.SERVER_UPDATE, OnServerUpdate );
            _socket.On( LobbyEvents.GAME_UPDATE, OnGameUpdate );
            
            _socket.Emit(LobbyEvents.UPDATE_LIST, (gameList) =>
            {
                //UpdateGameList(gameList["games"].list);
            });

            //connectingTimer.text = "";
        }

        public void OnPressedPlay()
        {
            OnPressedCreateRoom();
        }

        public void OnPressedGoToProfile()
        {
            stateMachine.SetState( ( int ) LobbyState.Profile );
        }

        private void OnPressedCreateRoom()
        {
            var user = ( stateMachine as LobbyController ).user;
            var gameName = $"{user}{DateTime.Now}";
            var json = new JSONObject();
            json.SetField("gameName", gameName);
            json.SetField("user", user);
     
            _socket.Emit(LobbyEvents.CREATE_GAME, json, (response) =>
            {
                //{"id":"030e2ab1-9fe6-49f7-b8fb-fe54b076c3bb",
                //"name":"Game 1","gameUsers":[{"id":"28e51e1d-75fd-435f-8487-97e3e04d4996","name":"WindowsEditor-MSI","icon":"X"}]}
                var users = response.list.First()["gameUsers"].list;
                currentGameName = response.list.First()["name"].str;
                currentGameId = response.list.First()["id"].str;
                //UpdateUsers( users.Select( u => u["name"].str ) );
                //SetInRoom( currentGameName, true );
                Debug.Log($"CREATE_GAME {response}");

                OnPressedStartGame();
            });
        }

        private void OnPressedStartGame()
        {
            var user = ( stateMachine as LobbyController ).user;

            string encodedString = "{\"gameId\": \"previousGame\",\"previousGame\": \"none\"}";
        
            var json = new JSONObject(encodedString);
            json.SetField("user", user);
            json["gameId"].str = currentGameId;

     
            _socket.Emit(LobbyEvents.START_GAME, json, (response) =>
            {
                //{"id":"030e2ab1-9fe6-49f7-b8fb-fe54b076c3bb",
                //"name":"Game 1","gameUsers":[{"id":"28e51e1d-75fd-435f-8487-97e3e04d4996","name":"WindowsEditor-MSI","icon":"X"}]}
                // var users = response.list.First()["gameUsers"].list;
                // currentGameName = response.list.First()["name"].str;
                // currentGameId = response.list.First()["id"].str;
                // _uiController.Lobby.UpdateUsers( users.Select( u => u["name"].str) );
                // _uiController.Lobby.SetInRoom(currentGameName, true);
                Debug.Log($"START_GAME {response}");
            });
        }
    
        private void OnServerUpdate( SocketIOEvent obj )
        {
            var gamesData = ParseGamesList(obj.data.ToString());
            if( gamesData != null ) UpdateGameList(gamesData);

            var games = obj.data?["games"].list;
            Debug.Log($"SERVER_UPDATE {games?.Count} {games}");
        }

        private void OnGameUpdate(SocketIOEvent obj)
        {
            var gameData = ParseGame(obj.data.ToString());
            if( gameData != null && gameData.id == currentGameId )
            {
                var game = obj.data;
                Debug.Log($"{LobbyEvents.GAME_UPDATE} {game}");
            
                //_uiController.Lobby.UpdateConnectionStatus(LobbyUI.ConnectionStatus.WaitForGameStart);
                SetTimer( gameData.serverInfo.time );

                //GlobalSettings.ServerAddress = gameData.serverInfo.address;
                GlobalSettings.ServerPort = (ushort)gameData.serverInfo.port;
            
                StartCoroutine(FinalCountdown(gameData.serverInfo.time));
            }
        }

        private IEnumerator FinalCountdown(float time)
        {
            while (time > 0)
            {
                yield return new WaitForSeconds(1);
                time -= 1;
                SetTimer((int)time);
            }

            SceneManager.LoadScene( 1 );
        }

        private void SetTimer( int time )
        {
            connectingTimer.text = time > 0 ? time.ToString() : "";
            
            //startGameButton.interactable = false;
        }

        private GameData ParseGame(string str)
        {
            if (str.StartsWith("["))
            {
                str = str.TrimStart(new char[] {'['});
                str = str.TrimEnd(new char[] {']'});
            }
            fsSerializer fsSerializer = new fsSerializer();
            GameData gameData = null;
        
            fsResult result = fsJsonParser.Parse(str, out fsData fsData);
            if (result.Succeeded)
            {
                result = fsSerializer.TryDeserialize(fsData, ref gameData);
                if (!result.Succeeded) Debug.LogError($"ParseGame TryDeserialize fail {result.FormattedMessages}");
            }else Debug.LogError($"ParseGame Parse fail {result.FormattedMessages}");

            return gameData;
        }

        private void UpdateGameList(GamesData gamesData)
        {
            Debug.Log($"UpdateGameList {gamesData.games.Length}");
        
            _games = gamesData.games;
        
            // UpdateRooms( _games );
        }

        private GamesData ParseGamesList(string str)
        {
            if (str.StartsWith("["))
            {
                str = str.TrimStart(new char[] {'['});
                str = str.TrimEnd(new char[] {']'});
            }
            fsSerializer fsSerializer = new fsSerializer();
            GamesData gamesData = null;
        
            fsResult result = fsJsonParser.Parse(str, out fsData fsData);
            if (result.Succeeded)
            {
                result = fsSerializer.TryDeserialize(fsData, ref gamesData);
                if (!result.Succeeded) Debug.LogError($"ParseGamesList TryDeserialize fail {result.FormattedMessages}");
            }else Debug.LogError($"ParseGamesList Parse fail {result.FormattedMessages}");

            return gamesData;
        }
    }
}