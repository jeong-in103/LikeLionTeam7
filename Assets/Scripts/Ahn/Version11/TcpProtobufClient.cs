using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using Game;

public class TcpProtobufClient : MonoBehaviour
{
    public static TcpProtobufClient Instance { get; private set; }
    
    private TcpClient tcpClient;
    private NetworkStream stream;
    private bool isRunning = false;

    private const string SERVER_IP = "127.0.0.1";
    private const int SERVER_PORT = 9090;

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
        ConnectToServer();
        SendLoginMessage(SuperManager.Instance.playerId);
    }

    void ConnectToServer()
    {
        try
        {
            tcpClient = new TcpClient(SERVER_IP, SERVER_PORT);
            stream = tcpClient.GetStream();
            isRunning = true;
            StartReceiving();
            Debug.Log("Connected to server.");

            // 연결 상태 추가 로깅
            Debug.Log($"Client connected: {tcpClient.Connected}");
            Debug.Log($"Stream available: {stream != null}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error connecting to server: {e.Message}");
        }
    }

    void StartReceiving()
    {
        try
        {
            Debug.Log("Starting to receive messages...");
            byte[] lengthBuffer = new byte[4];
            stream.BeginRead(lengthBuffer, 0, 4, OnLengthReceived, lengthBuffer);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting receive: {e.Message}");
            isRunning = false;
        }
    }

    void OnLengthReceived(IAsyncResult ar)
    {
        try
        {
            if (!tcpClient.Connected)
            {
                Debug.LogError("Connection lost during receive");
                return;
            }

            int bytesRead = stream.EndRead(ar);
            //Debug.Log($"Received length bytes: {bytesRead}");

            if (bytesRead == 0)
            {
                Debug.LogWarning("Connection closed by server");
                return;
            }

            byte[] lengthBuffer = (byte[])ar.AsyncState;
            int length = BitConverter.ToInt32(lengthBuffer, 0);
            //Debug.Log($"Expecting message of length: {length}");

            byte[] messageBuffer = new byte[length];
            stream.BeginRead(messageBuffer, 0, length, OnMessageReceived, messageBuffer);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in OnLengthReceived: {e.Message}");
            isRunning = false;
        }
    }

    public void SendMonsterDamage(int monsterId, float damage, float currentHp)
    {
        var damageMessage = new MonsterDamage
        {
            MonsterId = monsterId,
            Damage = damage,
            CurrentHp = (int)currentHp
        };

        var message = new GameMessage
        {
            MonsterDamage = damageMessage
        };

        SendMessage(message);
    }
    
    void OnMessageReceived(IAsyncResult ar)
    {
        try
        {
            int bytesRead = stream.EndRead(ar);
            if (bytesRead == 0) return; // 연결 종료

            byte[] messageBuffer = (byte[])ar.AsyncState;
            GameMessage gameMessage = GameMessage.Parser.ParseFrom(messageBuffer);
            UnityMainThreadDispatcher.Instance.Enqueue(gameMessage);

            StartReceiving(); // 다음 메시지 수신 대기
        }
        catch (Exception e)
        {
            Debug.LogError($"Error receiving message: {e.Message}");
        }
    }
    
    public void SendPlayerPosition(string playerId, float x, float y, float z, float vx, float vy, float vz, float speed, float rotationY)
    {
        var position = new PlayerPosition
        {
            PlayerId = playerId,
            X = x,
            Y = y,
            Z = z,
            Fx = vx,
            Fy = vy,
            Fz = vz,
            Speed = speed,
            RotationY = rotationY
        };
        var message = new GameMessage
        {
            PlayerPosition = position
        };
        SendMessage(message);
    }
    
    public void SendPlayerLogout(string playerId)
    {
        var msg = new LogoutMessage()
        {
            PlayerId = playerId,
        };
        var message = new GameMessage
        {
            Logout = msg
        };
        SendMessage(message);
    }   

    public void SendChatMessage(string sender, string content)
    {
        var chat = new ChatMessage
        {
            Sender = sender,
            Content = content
        };
        var message = new GameMessage
        {
            Chat = chat
        };
        SendMessage(message);
    }
    
    public void SendLoginMessage(string playerId)
    {
        var login = new LoginMessage()
        {
            PlayerId = playerId
        };
        var message = new GameMessage
        {
            Login = login
        };
        SendMessage(message);
    }

    public void SendSpawnMonstesZero()
    {
        SendSpawnMonster(0, 0, 1);
    }
    public void SendSpawnMonster(float x, float z, int monsterId)
    {
        var spawnMonster = new SpawnMonster
        {
            X = x,
            Z = z,
            MonsterId = monsterId
        };
    
        var message = new GameMessage
        {
            SpawnMonster = spawnMonster
        };
    
        SendMessage(message);
    }

    public void SendActionMessage(GameMessage message)
    {
        //만약 액션 반환값에 필요한 다른 데이터가 더 있다면 여기서 추가
        SendMessage(message);
    }

    
    private void SendMessage(GameMessage message)
    {
        try 
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                byte[] messageBytes = message.ToByteArray();
                byte[] lengthBytes = BitConverter.GetBytes(messageBytes.Length);

                Debug.Log($"Sending message of type: {message.MessageCase}, length: {messageBytes.Length}");
            
                stream.Write(lengthBytes, 0, 4);
                stream.Write(messageBytes, 0, messageBytes.Length);
                stream.Flush(); // 스트림 즉시 전송 보장
            
                Debug.Log("Message sent successfully");
            }
            else
            {
                Debug.LogError("Cannot send message - client is not connected");
                // 재연결 시도
                ConnectToServer();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending message: {e.Message}");
            isRunning = false;
            // 재연결 시도
            ConnectToServer();
        }
    }
    
    public void SendPlayerDamage(string playerId, float damage, int attackType, float hitX, float hitY, float hitZ)
    {
        var damageMessage = new PlayerDamage
        {
            PlayerId = playerId,
            Damage = damage,
            AttackType = attackType,
            HitPointX = hitX,
            HitPointY = hitY,
            HitPointZ = hitZ
        };

        var message = new GameMessage
        {
            PlayerDamage = damageMessage
        };

        SendMessage(message);
    }
    
    
    void OnDisable()
    {
        try
        {
            isRunning = false;
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient = null;
            }
            Debug.Log("Client connection closed properly");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during cleanup: {e.Message}");
        }
    }
    
    
    
}