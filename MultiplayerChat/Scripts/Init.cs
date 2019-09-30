using Godot;
using System;
using System.Collections.Generic;

public class Init : Control{
    private int port = 8920;
    private int maxPlayers = 4;//server + 4 clients

    //data
    private Dictionary<int,DataPlayer> data;//other clients
    private DataPlayer myData;//localData

    //views
    private Control panelStart;
    private LineEdit lineIP, linePort, lineMax, lineName;

    private Panel panelChat;
    private RichTextLabel chat;
    private Control back;
    private LineEdit lineIn;

    private RichTextLabel clients;

    //START
    public override void _Ready(){
        GD.Print("PLAY!");

        panelStart = GetNode("start") as Control;
        lineIP = GetNode("start/panel/lineIP") as LineEdit;
        linePort = GetNode("start/panel/linePort") as LineEdit;
        lineMax = GetNode("start/panel/lineMax") as LineEdit;
        lineName = GetNode("start/panel/lineName") as LineEdit;

        panelChat = GetNode("chat") as Panel;
        chat = GetNode("chat/txt") as RichTextLabel;
        back = GetNode("chat/back") as Control;
        back.SetVisible (false);
        lineIn = GetNode("chat/back/in") as LineEdit;
        lineIn.SetText("");
        clients = GetNode("chat/clients/clients") as RichTextLabel;

        //SIGNALS FOR NETWORK CALLBACKS
        GetTree().Connect("network_peer_connected",this,"peerConnected");
        GetTree().Connect("network_peer_disconnected",this,"peerDisconnected");
        GetTree().Connect("connected_to_server",this,"connectedToServer");
        GetTree().Connect("server_disconnected",this,"serverDisconnected");
        GetTree().Connect("connection_failed",this,"connectedFail");

        //DEFAULT CLIENT DATA:
        data = new Dictionary<int, DataPlayer>();
        myData = new DataPlayer(0,"Null");
    }

    //ONCLICK START CLIENT OR SERVER:
    public void onClickHost(){
        //IF NEED WORLD, LOAD HERE (BEFORE START SERVER)
        //LoadWorld();

        //Get view data
        if (!int.TryParse(linePort.GetText(),out port)){
            port = 8920;
        }

        if (!int.TryParse(lineMax.GetText(),out maxPlayers)){
            maxPlayers = 4;//4 when parse fail
        }

        //CREATE SERVER
        var peer = new NetworkedMultiplayerENet();
        Error err = peer.CreateServer(port,maxPlayers);
        
        if (err != Error.Ok){
            GD.Print("YOU CAN NOT CREATE SERVER:" + err.ToString() );
            return;
        }
        GetTree().SetNetworkPeer(peer);

        //set View
        panelChat.SelfModulate = new Color(1f,0,0);//red style
        panelStart.SetVisible(false);//server cant use chat

        chat.BbcodeText = ("[color=red]SERVER READY![/color]");

    }

    public void onClickJoin(){
        //get data
        string ip = lineIP.GetText();

        if (!int.TryParse(linePort.GetText(),out port)){
            port = 8920;
        }

        string name = lineName.GetText();
        if (name.Empty()) name = "NoName";
        
        myData = new DataPlayer(0, name);//data model example
        myData.color = new Random().Next(0,3); //de 0,1,2

        //view
        panelStart.SetVisible(false);
        panelChat.SelfModulate = new Color(.2f,.2f,0.5f);//blue style
        back.SetVisible (true);
        chat.BbcodeText = ("[color=purple]Connecting to " + ip + " : " + port + " ...[/color]");

        //CONNECTION
        var peer = new NetworkedMultiplayerENet();
        peer.CreateClient(ip,port);
        GetTree().SetNetworkPeer(peer);

        myData.id = GetTree().GetNetworkUniqueId();//get ID

    }

    //NET CALLBACKS
    public void peerConnected(int id){
        if (GetTree().IsNetworkServer()) addChatText("\n•New player. ID: " + id); 
        //SERIALIZE DATA TO STRING (User other serialize format: xml, json, bytes...)
        string datos = string.Format("{0},{1},{2}", myData.id, myData.name, myData.color);
        //sinc dictionaries
        RpcId(id,"registerPlayer", datos);
    }

    public void peerDisconnected(int id){
        //show bye
        addChatText(string.Format("\n•{0} bye!",data[id].name)); 
        //delete other players nodes here
        //update client list
        data.Remove(id);
        //to view
        updatePlayerListView();
    }

    public void connectedToServer(){
        //HI
        addChatText("\nConnected!");
        Rpc("sendTextChat", string.Format("{0} is online!", myData.name));
        //LOAD WORLD AND LOCAL PLAYER
    }

    public void serverDisconnected(){
        //server shutdown
         chat.BbcodeText = ("Server disconnect!");
         disconnect();
    }

    public void connectedFail(){
        //Only called on clients
        chat.BbcodeText = ("Connection Fail!");
        disconnect();
    }

    private void disconnect(){
        //REMOVE WORLD AND PLAYERS HERE
        GetTree().SetNetworkPeer(null);
        panelStart.SetVisible(true);
        back.SetVisible (false);
        data = null;
        updatePlayerListView();
    }


    //VIEW
    public void updatePlayerListView(){
        //delete list
        clients.BbcodeText = "";
        if (data == null) return;
        //dictionary data to labels(hide server on list)
        foreach (DataPlayer p in data.Values){
            if (p.name == "Null") continue;
            clients.AppendBbcode(dataPlayerToList(p));
        }
        //last: my data to label (server hide)
        if (myData.name != "Null"){
            clients.AppendBbcode (dataPlayerToList(myData));
        }
        
    }

    private string dataPlayerToList(DataPlayer p){
        if (p == null) return null; //my color?
        string strColor = dataColorToString(p.color); //format bbcode
        return string.Format("•[url={0}][color={1}]{2}[/color][/url]\n",p.id,strColor,p.name);
    }

    private string dataColorToString(int intColor){
        string strColor = "#ffffff";      
        switch(intColor){
            case 0: strColor = "red"; break;
            case 1: strColor = "green"; break;
            case 2: strColor = "aqua"; break;
        }
        return strColor;
    }

    private void addChatText(string txt){
        const int maxLines = 50;
        chat.AppendBbcode(txt);
        //control max lines: happy ram
        int count = chat.GetLineCount();        
        if (count > maxLines){
            int rest = count - maxLines;
            for (int i = 0; i<rest; i++){
                chat.RemoveLine(i);
            }
        }
    }
    
    //ON CLICKS
    public void onClickExit(){
        chat.BbcodeText = ("BYE BYE!");
        disconnect();
        GetTree().Quit();//exit game
    }

    public void onTextEntered (string text){
        onClickSend();
    }

    public void onClickSend(){
        lineIn.GrabFocus();//focus to lineIn
        if (lineIn.Text.Empty()) return;//no empty
        string strColor = dataColorToString(myData.color);//my color?
        //format bbcode
        string txt = string.Format("[color={0}]-{1}: {2}[/color]",strColor,myData.name, lineIn.Text);
        Rpc("sendTextChat", txt);//send to all
        lineIn.SetText("");//clear inputfield
    }

     public void onClientListClickMeta(string meta){
        int id = int.Parse(meta);
        DataPlayer dp = null;
        if (!data.TryGetValue(id, out dp)){
            if(id == myData.id){
                dp = myData;
            }else{
                GD.Print("No data, id: " + meta);
                return;
            }    
        }
        //show only in local
        addChatText(string.Format(
            "\n•id: {0} name: {1} color: {2}",
            dp.id,
            dp.name,
            dp.color));
    }

    //REMOTES
    [Remote]
    public void registerPlayer(string datos){
        //string to DataModel
        string[] subParte = datos.Split(",",true);
        DataPlayer dataP = new DataPlayer(int.Parse(subParte[0]),subParte[1]);
        dataP.color = int.Parse(subParte[2]);
        //to dic
        int senderId = GetTree().GetRpcSenderId();
        data[senderId] = dataP;
        //to view
        updatePlayerListView();
        //create other player here
    }

    [RemoteSync]
    public void sendTextChat(string line){
        addChatText(string.Format("\n{0}",line));
    }
}

public class DataPlayer{
    public int id;
    public string name;
    public int color;

    public DataPlayer(int id, string name){
        this.id = id;
        this.name = name;
        color = 0;
    }

}