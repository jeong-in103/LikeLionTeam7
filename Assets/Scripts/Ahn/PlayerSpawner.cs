using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Game;
using Unity.Collections;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    private Player myPlayer;
    private PlayerController myPlayerCtrl;
    private Dictionary<string, OtherPlayer> _otherPlayers = new();

    public Transform SpawnPosition;
    public GameObject MyPlayerTemplate;
    public GameObject OtherPlayerTemplate;

    public CinemachineVirtualCamera cam;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        
    }

    public void SpawnMyPlayer(Vector3 spawnPos)
    {
        if (spawnPos == Vector3.zero)
            spawnPos = SpawnPosition.position;
        
        GameObject SpawnPlayer = Instantiate(MyPlayerTemplate.gameObject, spawnPos, Quaternion.identity);
        myPlayer = SpawnPlayer.GetComponent<Player>();
        myPlayerCtrl = SpawnPlayer.GetComponent<PlayerController>();
        cam.Follow = SpawnPlayer.transform.GetChild(0);
        cam.LookAt = SpawnPlayer.transform.GetChild(0);
        myPlayer.Initialize(SuperManager.Instance.playerId);
    }

    public void SpawnOtherPlayer(string playerId, Vector3 spawnPos, float spawnRot)
    {
        GameObject spawnPlayer = Instantiate(OtherPlayerTemplate.gameObject, spawnPos, Quaternion.identity);
        OtherPlayer otherPlayer = spawnPlayer.GetComponent<OtherPlayer>();
        otherPlayer.transform.position = spawnPos;
        otherPlayer.transform.rotation = Quaternion.Euler(0.0f, spawnRot, 0.0f);
        _otherPlayers.Add(playerId, otherPlayer);
        otherPlayer.Initialize(playerId);
    }
    
    public void DestroyOtherPlayer(string playerId)
    {
        if (_otherPlayers.TryGetValue(playerId, out OtherPlayer otherPlayer))
        {
            Destroy(otherPlayer.gameObject);
            _otherPlayers.Remove(playerId);
        }
    }
    
    public void OnOtherPlayerPositionUpdate(PlayerPosition playerPosition)
    {
        if (_otherPlayers.TryGetValue(
                playerPosition.PlayerId, out OtherPlayer otherPlayer))
        {
            otherPlayer.UpdatePlayerPosition(playerPosition);
        }
    }
    
    public Transform GetPlayerTransform(string playerId)
    {
        // 자신의 플레이어인 경우
        if (myPlayer != null && playerId == myPlayer.PlayerId)
        {
            return myPlayer.transform;
        }
        
        // 다른 플레이어인 경우
        if (_otherPlayers.TryGetValue(playerId, out OtherPlayer otherPlayer))
        {
            return otherPlayer.transform;
        }
        
        return null;
    }
    
    public Player GetMyPlayer()
    {
        return myPlayer;
    }
    
    public PlayerController GetMyPlayerController()
    {
        return myPlayerCtrl;
    }

    public bool TryGetOtherPlayer(string playerId, out OtherPlayer otherPlayer)
    {
        return _otherPlayers.TryGetValue(playerId, out otherPlayer);
    }
}
