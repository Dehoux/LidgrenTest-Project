// Socket Policy Server (sockpol)
//
// Copyright (C) 2009 Novell, Inc (http://www.novell.com)
//
// Based on XSP source code (ApplicationServer.cs and XSPWebSource.cs)
// Authors:
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Lluis Sanchez Gual (lluis@ximian.com)
//
// Copyright (c) Copyright 2002-2007 Novell, Inc
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class SocketPolicyServer {

	const string PolicyFileRequest = "<policy-file-request/>";
	static byte[] _request = Encoding.UTF8.GetBytes (PolicyFileRequest);
	private byte[] _policy;
	private int _policyPort = 843;
	private Socket _listenSocket;
	private Thread _runner;

	private AsyncCallback _acceptCb;
	
	public int PolicyPort
		{
			get { return _policyPort; }
			set { _policyPort = value; }
		}
	
	class Request {
		public Request (Socket s)
		{
			Socket = s;
			// the only answer to a single request (so it's always the same length)
			Buffer = new byte [_request.Length];
			Length = 0;
		}

		public Socket Socket { get; private set; }
		public byte[] Buffer { get; set; }
		public int Length { get; set; }
	}
	
	public SocketPolicyServer (string xml)
	{
		// transform the policy to a byte array (a single time)
		_policy = Encoding.UTF8.GetBytes (xml);
	}
	
	public SocketPolicyServer (string xml, int port) 
		:this(xml)
	{
		_policyPort = port;
	}

	public int Start ()
	{
		int code=0;
		try {
			_listenSocket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			_listenSocket.Bind (new IPEndPoint (IPAddress.Any, _policyPort));
			_listenSocket.Listen (500);
			_listenSocket.Blocking = false;
			Console.WriteLine(" Policy service started on Port: "+_policyPort);
		}
		catch (SocketException se) {
			code=1;
			// Most common mistake: port 843 is not user accessible on unix-like operating systems
			if (se.SocketErrorCode == SocketError.AccessDenied) {
				Console.WriteLine (" NOTE: must be run as root if listen is <1024 to port "+_policyPort);
				code=5;
			} else {
				code=6;
			}
			Console.WriteLine (" Failed to start policy server: "+se.Message.ToString());
			return code;
		}

		_runner = new Thread (new ThreadStart (RunServer));
		_runner.Start ();
		return code;
	}

	void RunServer ()
	{
		_acceptCb = new AsyncCallback (OnAccept);
		_listenSocket.BeginAccept (_acceptCb, null);

		while (true) // Just sleep until we're aborted.
			Thread.Sleep (1000000);
	}

	void OnAccept (IAsyncResult ar)
	{
		Console.WriteLine("incoming connection");
		Socket accepted = null;
		try {
			accepted = _listenSocket.EndAccept (ar);
		} catch {
		} finally {
			_listenSocket.BeginAccept (_acceptCb, null);
		}

		if (accepted == null)
			return;

		accepted.Blocking = true;

		Request request = new Request (accepted);
		accepted.BeginReceive (request.Buffer, 0, request.Length, SocketFlags.None, new AsyncCallback (OnReceive), request);
	}

	void OnReceive (IAsyncResult ar)
	{
		Request r = (ar.AsyncState as Request);
		Socket socket = r.Socket;
		try {
			r.Length += socket.EndReceive (ar);

			// compare incoming data with expected request
			for (int i=0; i < r.Length; i++) {
				if (r.Buffer [i] != _request [i]) {
					// invalid request, close socket
					socket.Close ();
					return;
				}
			}

			if (r.Length == _request.Length) {
				Console.WriteLine("got policy request, sending response");
				// request complete, send policy
				socket.BeginSend (_policy, 0, _policy.Length, SocketFlags.None, new AsyncCallback (OnSend), socket);
			} else {
				// continue reading from socket
				socket.BeginReceive (r.Buffer, r.Length, _request.Length - r.Length, SocketFlags.None, 
					new AsyncCallback (OnReceive), ar.AsyncState);
			}
		} catch {
			// if anything goes wrong we stop our connection by closing the socket
			socket.Close ();
		}
        }

	void OnSend (IAsyncResult ar)
        {
		Socket socket = (ar.AsyncState as Socket);
		try {
			socket.EndSend (ar);
		} catch {
			// whatever happens we close the socket
		} finally {
			socket.Close ();
		}
	}

	public void Stop ()
	{
		_runner.Abort ();
		_listenSocket.Close ();
		Console.WriteLine("");
		Console.WriteLine("  Policy service stopped.");
	}

	const string AllPolicy = 

@"<?xml version='1.0'?>
<cross-domain-policy>
        <allow-access-from domain=""*"" to-ports=""*"" />
</cross-domain-policy>";

	const string LocalPolicy = 

@"<?xml version='1.0'?>
<cross-domain-policy>
	<allow-access-from domain=""*"" to-ports=""4500-4550"" />
</cross-domain-policy>";

    //static int Main (string[] args)
    //{
    //    if (args.Length == 0) {
    //        Console.WriteLine ("sockpol [--all | --range | --file policy]");
    //        Console.WriteLine ("\t--all	Allow access on every port)");
    //        Console.WriteLine ("\t--range	Allow access on portrange 4500-4550)");
    //        return 1;
    //    }

    //    string policy = null;
    //    switch (args [0]) {
    //    case "--all":
    //        policy = AllPolicy;
    //        break;
    //    case "--local":
    //        policy = LocalPolicy;
    //        break;
    //    case "--file":
    //        if (args.Length < 2) {
    //            Console.WriteLine ("Missing policy file name after '--file'.");
    //            return 2;
    //        }
    //        string filename = args [1];
    //        if (!File.Exists (filename)) {
    //            Console.WriteLine ("Could not find policy file '{0}'.", filename);
    //            return 3;
    //        }
    //        using (StreamReader sr = new StreamReader (filename)) {
    //            policy = sr.ReadToEnd ();
    //        }
    //        break;
    //    default:
    //        Console.WriteLine ("Unknown option '{0}'.", args [0]);
    //        return 4;
    //    }

    //    SocketPolicyServer server = new SocketPolicyServer (policy);
    //    int result = server.Start ();
    //    if (result != 0)
    //        return result;

    //    Console.WriteLine ("Hit Return to stop the server.");
    //    Console.ReadLine ();
    //    server.Stop ();
    //    return 0;
    //}
}
