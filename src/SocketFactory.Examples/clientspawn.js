class TextPacket {
    constructor()
	{
		this.Message = "";
	}
}

class ClientSpawn {

	constructor(onUpdate, onClose, onOpen, port) {
		this._isConnected = new Boolean(false);
		this._onUpdate = onUpdate;
		this._onClose = onClose;
		this._onOpen = onOpen;
		this._port = port;
		this._socket = null;
	}
	
	connect()
	{
		ClientSpawn.connect(this);
	}
	
	static connect(spawn)
	{
		if(spawn._socket != null) {
			spawn._socket.close();
			spawn._socket = null;
		}
		
		if (!("WebSocket" in window))
		{
			console.error("WebSocket NOT supported by your Browser!");
			return;
		}

		spawn._socket = new WebSocket("ws://localhost:" + spawn._port + "/");
		
	    spawn._socket.onopen = function()
	    {
			spawn._isConnected = new Boolean(true);
			spawn._onOpen();
			console.log("socket is connected");
	    };
		
	    spawn._socket.onmessage = function (evt) 
	    { 
			var obj = JSON.parse(evt.data);
			spawn._onUpdate(obj);
	    };
		
	    spawn._socket.onclose = function()
	    { 
			spawn._socket = null;
			console.log("Attempting to reconnect..."); 
			spawn._isConnected = new Boolean(false);
			spawn._onClose();
			setTimeout(ClientSpawn.connect, 3000, spawn);
	    };
	}
	
	send(packet)
	{
		this._socket.send(packet);
	}
}