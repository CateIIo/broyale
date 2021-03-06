﻿
using UnityEngine;

namespace Scripts.Common
{
    public static class Constants
    {
        public const string SCENE_LOBBY = "lobby";
        public const string SCENE_CLIENT = "client";
        public const string SCENE_CLIENTANDSERVER = "previewclientserver";

        public static string GetDeviceID()
        {
            return Application.isEditor ? "Editor_" + SystemInfo.deviceUniqueIdentifier : SystemInfo.deviceUniqueIdentifier;
        }
    }
}