using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;
    public GameObject prefab;
    private string serverInternalId;
    List<string> ServerListId = new List<string>();

    private Dictionary<string, GameObject> clientList = new Dictionary<string, GameObject>();

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);
    }
    
    void SendToServer(string message)
    {
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");
       
        //Example to send a handshake message:
        Debug.Log("Sending HandShake");
        HandshakeMsg m = new HandshakeMsg();
        m.player.id = m_Connection.InternalId.ToString();
        m.player.cubeColor = new Color (1.0f,0.0f, 0.0f );
        m.player.cubPos = new Vector3();
        SendToServer(JsonUtility.ToJson(m));

        StartCoroutine(SendRepeatPlayerUpdate());
        StartCoroutine(ChangeColor());
        StartCoroutine(CheckForDroppedPlayers());

    }
    IEnumerator CheckForDroppedPlayers()
    {
        while (true)
        {
            foreach (KeyValuePair<string, GameObject> player in clientList)
            {
                if (!ServerListId.Contains(player.Key))
                {
                    Destroy(player.Value);
                    clientList.Remove(player.Key);
                }
            }
            yield return new WaitForSeconds(1);
        }
    }

    IEnumerator ChangeColor()
    {
        while (true)
        {
            yield return new WaitForSeconds(1);
            if (serverInternalId == null)
                yield return new WaitForSeconds(1);
            if (clientList.ContainsKey(serverInternalId))
                clientList[serverInternalId].GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0.0F, 1.0F), UnityEngine.Random.Range(0.0F, 1.0F), UnityEngine.Random.Range(0.0F, 1.0F));
        }
    }

    IEnumerator SendRepeatPlayerUpdate()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.2f);
            Debug.Log("Sending Update");
            PlayerUpdateMsg m = new PlayerUpdateMsg();
            m.player.id = serverInternalId;
            m.player.cubeColor = clientList[serverInternalId].GetComponent<Renderer>().material.color;
            m.player.cubPos = clientList[serverInternalId].transform.position;
            SendToServer(JsonUtility.ToJson(m));
        }
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch (header.cmd)
        {
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received! ID : " + hsMsg.player.id);
                serverInternalId = hsMsg.player.id;
                clientList.Add(hsMsg.player.id, Instantiate(prefab));
                
                break;
            //case Commands.PLAYER_UPDATE:
            //    PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            //    Debug.Log("Player update message received!");
            //    break;
            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);

                Debug.Log("Server update message received! ");
                Debug.Log("# of Players : " + suMsg.players.Count);
                

                List<string> tempListId = new List<string>();

                foreach (NetworkObjects.NetworkPlayer player in suMsg.players)
                {
                    tempListId.Add(player.id);
                    Debug.Log(" >>>>>> ID : " + player.id);
                    if (!clientList.ContainsKey(player.id))
                    {
                        clientList.Add(player.id, Instantiate(prefab));
                    }
                    if (player.id != serverInternalId)
                    {
                        clientList[player.id].transform.position = player.cubPos;
                        clientList[player.id].GetComponent<Renderer>().material.color = player.cubeColor;
                    }
                }
                ServerListId.Clear();
                ServerListId = tempListId;
                break;
            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }   
    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }


        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
        if (Input.GetKey(KeyCode.W))
        {
            clientList[serverInternalId].transform.Translate(new Vector3(0.0f, 0.1f, 0.0f));
        }
        if (Input.GetKey(KeyCode.S))
        {
            clientList[serverInternalId].transform.Translate(new Vector3(0.0f, -0.1f, 0.0f));
        }
        if (Input.GetKey(KeyCode.A))
        {
            clientList[serverInternalId].transform.Translate(new Vector3(-0.1f, 0.0f, 0.0f));
        }
        if (Input.GetKey(KeyCode.D))
        {
            clientList[serverInternalId].transform.Translate(new Vector3(0.1f, 0.0f, 0.0f));
        }

        
    }



}