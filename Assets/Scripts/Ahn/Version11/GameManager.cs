﻿using UnityEngine;
using System.Collections.Generic;
using Game;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameObject monsterPrefab;
    private Dictionary<int, MonsterController> monsters = new Dictionary<int, MonsterController>();

    // NavMesh visualization
    public LineRenderer pathRenderer;
    private List<Vector3> currentPath = new List<Vector3>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnApplicationQuit()
    {
        TcpProtobufClient.Instance.SendPlayerLogout(SuperManager.Instance.playerId);
    }

    private void Update()
    {
        ProcessMessageQueue();
    }

    private void ProcessMessageQueue()
    {
        while (UnityMainThreadDispatcher.Instance.ExecutionQueue.Count > 0)
        {
            GameMessage msg = UnityMainThreadDispatcher.Instance.ExecutionQueue.Dequeue();
            ProcessMessage(msg);
        }
    }

    private void ProcessMessage(GameMessage msg)
    {
        Debug.Log("msg : " + msg.MessageCase);
        switch (msg.MessageCase)
        {
            case GameMessage.MessageOneofCase.PlayerPosition:
                PlayerSpawner.Instance.OtherPlayerPositionUpdate(msg.PlayerPosition);
                break;
            case GameMessage.MessageOneofCase.SpawnMyPlayer:
                var mySpawnPos = new Vector3(msg.SpawnMyPlayer.X, msg.SpawnMyPlayer.Y, msg.SpawnMyPlayer.Z);
                PlayerSpawner.Instance.SpawnMyPlayer(msg.SpawnMyPlayer.PlayerTemplate, mySpawnPos);
                break;
            case GameMessage.MessageOneofCase.SpawnOtherPlayer:
                var otherSpawnPos = new Vector3(msg.SpawnOtherPlayer.X, msg.SpawnOtherPlayer.Y, msg.SpawnOtherPlayer.Z);
                var otherSpawnRot = msg.SpawnOtherPlayer.RotationY;
                PlayerSpawner.Instance.SpawnOtherPlayer(msg.SpawnOtherPlayer.PlayerId, msg.SpawnOtherPlayer.PlayerTemplate, otherSpawnPos, otherSpawnRot);
                break;
            case GameMessage.MessageOneofCase.Logout:
                PlayerSpawner.Instance.DestroyOtherPlayer(msg.Logout.PlayerId);
                break;
            case GameMessage.MessageOneofCase.SpawnMonster:
                HandleSpawnMonster(msg.SpawnMonster);
                break;
            case GameMessage.MessageOneofCase.MoveMonster:
                HandleMoveMonster(msg.MoveMonster);
                break;
            case GameMessage.MessageOneofCase.MonsterTarget:
                HandleMonsterTarget(msg.MonsterTarget);
                break;
            case GameMessage.MessageOneofCase.PathTest:
                HandlePathTest(msg.PathTest);
                break;
            case GameMessage.MessageOneofCase.MonsterAttack:
                HandleMonsterAttack(msg.MonsterAttack);
                break;
            case GameMessage.MessageOneofCase.MeteorStrike:
                if (monsters.TryGetValue(msg.MeteorStrike.MonsterId, out MonsterController monster))
                    monster.HandleMeteorStrike(msg.MeteorStrike);
                break;
            case GameMessage.MessageOneofCase.MonsterDamage:
                HandleMonsterDamage(msg.MonsterDamage);
                break;
            case GameMessage.MessageOneofCase.PlayerDamage:
                HandlePlayerDamage(msg.PlayerDamage);
                break;
            case GameMessage.MessageOneofCase.MonsterRotate:
                HandleMonsterRotate(msg.MonsterRotate);
                break; 
            case GameMessage.MessageOneofCase.AnimatorSetInteger:
                PlayerSpawner.Instance.OtherPlayerAnimatorUpdate(msg.AnimatorSetInteger.PlayerId, msg.AnimatorSetInteger.AnimId, msg.AnimatorSetInteger.Condition);
                break;
            case GameMessage.MessageOneofCase.AnimatorSetFloat:
                PlayerSpawner.Instance.OtherPlayerAnimatorUpdate(msg.AnimatorSetFloat.PlayerId, msg.AnimatorSetFloat.AnimId, msg.AnimatorSetFloat.Condition);
                break;
            case GameMessage.MessageOneofCase.AnimatorSetBool:
                PlayerSpawner.Instance.OtherPlayerAnimatorUpdate(msg.AnimatorSetBool.PlayerId, msg.AnimatorSetBool.AnimId, msg.AnimatorSetBool.Condition);
                break;        
            case GameMessage.MessageOneofCase.AnimatorSetTrigger:
                PlayerSpawner.Instance.OtherPlayerAnimatorUpdate(msg.AnimatorSetTrigger.PlayerId, msg.AnimatorSetTrigger.AnimId);
                break;   
            case GameMessage.MessageOneofCase.ApplyRootMotion:
                PlayerSpawner.Instance.OtherPlayerApplyRootMotion(msg.ApplyRootMotion.PlayerId,
                    msg.ApplyRootMotion.RootMosion);
                break;
            case GameMessage.MessageOneofCase.MonsterHitEffect:
                HandleMonsterHitEffect(msg.MonsterHitEffect);
                break;
        }
    }

    
    private void HandleMonsterHitEffect(MonsterHitEffect hitEffect)
    {
        if (monsters.TryGetValue(hitEffect.MonsterId, out MonsterController monster))
        {
            Vector3 hitPoint = new Vector3(
                hitEffect.HitPoint.X,
                hitEffect.HitPoint.Y,
                hitEffect.HitPoint.Z
            );
            Vector3 hitNormal = new Vector3(
                hitEffect.HitNormal.X,
                hitEffect.HitNormal.Y,
                hitEffect.HitNormal.Z
            );
   
            monster.PlayHitEffect(
                hitPoint,
                hitNormal,
                (DamageType)hitEffect.HitEffectType
            );
        }
    }
    
    
    private void HandleMonsterRotate(MonsterRotate rotateData)
    {
        if (monsters.TryGetValue(rotateData.MonsterId, out MonsterController monster))
        {
            monster.UpdateRotation(rotateData.Rotation, rotateData.Duration);
        }
    }

    private void HandlePlayerDamage(PlayerDamage playerDamage)
    {
        Debug.Log($"Received damage for player: {playerDamage.PlayerId}");
        Debug.Log($"Current player ID: {SuperManager.Instance.playerId}");
        
        // 내 플레이어가 맞은 경우
        if (playerDamage.PlayerId == SuperManager.Instance.playerId)
        {
            Player myPlayer = PlayerSpawner.Instance.GetMyPlayer();
            PlayerController myPlayerCtrl = PlayerSpawner.Instance.GetMyPlayerController();
            if (myPlayer != null)
            {
                Debug.Log($"Applying {playerDamage.AttackType} damage: {playerDamage.Damage} to player: {myPlayer.PlayerId}");
                Vector3 hitPoint = new Vector3(
                    playerDamage.HitPointX,
                    playerDamage.HitPointY,
                    playerDamage.HitPointZ
                );

                Vector3 hitNormal = hitPoint - myPlayerCtrl.transform.position;
                myPlayerCtrl.GetComponent<PlayerEffectManager>().PlayDamaged(hitPoint, hitNormal);
                hitNormal.y = 0f;
                myPlayerCtrl.LivingEntity.ApplyDamage(playerDamage.Damage, hitNormal);
            }
        }
        else
        {
            if (PlayerSpawner.Instance.TryGetOtherPlayer(playerDamage.PlayerId, out OtherPlayer otherPlayer))
            {
                Vector3 hitPoint = new Vector3(
                    playerDamage.HitPointX,
                    playerDamage.HitPointY,
                    playerDamage.HitPointZ
                );
                
                Vector3 hitNormal = hitPoint - otherPlayer.transform.position;

                if (EffectManager.Instance != null)
                {
                    // 피격 효과 재생
                    otherPlayer.GetComponent<PlayerEffectManager>().PlayDamaged(hitPoint, hitNormal);
                    hitNormal.y = 0f;
                    otherPlayer.LivingEntity.ApplyDamage(playerDamage.Damage, hitNormal);
                }
            }
        }
    }
    
    private void HandleSpawnMonster(SpawnMonster spawnData)
    {
        Vector3 spawnPosition = new Vector3(spawnData.X, 0, spawnData.Z);
        GameObject monsterObj = Instantiate(monsterPrefab, spawnPosition, 
            Quaternion.Euler(0, spawnData.RotationY * Mathf.Rad2Deg, 0));
        
        MonsterController controller = monsterObj.GetComponent<MonsterController>();
        controller.Initialize(spawnData.MonsterId);
        monsters[spawnData.MonsterId] = controller;
    }

    private void HandleMoveMonster(MoveMonster moveData)
    {
        Debug.Log("HandleMoveMonster msg : "+moveData.X+" , " +moveData.Z);
        if (monsters.TryGetValue(moveData.MonsterId, out MonsterController monster))
        {
            monster.UpdatePosition(new Vector3(moveData.X, 0, moveData.Z));
        }
    }

    private void HandleMonsterTarget(MonsterTarget targetData)
    {
        if (monsters.TryGetValue(targetData.MonsterId, out MonsterController monster))
        {
            monster.UpdateTarget(targetData.TargetPlayerId, targetData.HasTarget);
        }
    }

    private void HandlePathTest(PathTest pathTest)
    {
        currentPath.Clear();
        foreach (var point in pathTest.Paths)
        {
            currentPath.Add(new Vector3(point.X, point.Y, point.Z));
        }

        if (pathRenderer != null && currentPath.Count > 0)
        {
            pathRenderer.positionCount = currentPath.Count;
            pathRenderer.SetPositions(currentPath.ToArray());
        }
    }
    
    private void HandleMonsterAttack(MonsterAttack attackData)
    {
        if (monsters.TryGetValue(attackData.MonsterId, out MonsterController monster))
        {
            monster.PerformAttack(
                attackData.TargetPlayerId,
                attackData.AttackType,  // 공격 타입
                attackData.Damage       // 데미지
            );
        }
    }
    
    private void HandleMonsterDamage(MonsterDamage damageMsg)
    {
        if (monsters.TryGetValue(damageMsg.MonsterId, out MonsterController monster))
        {
            monster.SetHealth(damageMsg.CurrentHp);
        }
    }
}