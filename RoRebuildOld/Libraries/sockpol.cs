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

public class SocketPolicyServer {

	const string PolicyFileRequest = "<policy-file-request/>";
	static byte[] request = Encoding.UTF8.GetBytes (PolicyFileRequest);
	private byte[] policy;

	private Socket listen_socket;
	private Thread runner;

	private AsyncCallback accept_cb;

	class Request {
		public Request (Socket s)
		{
			Socket = s;
			// the only answer to a single request (so it's always the same length)
			Buffer = new byte [request.Length];
			Length = 0;
		}

		public Socket Socket { get; private set; }
		public byte[] Buffer { get; set; }
		public int Length { get; set; }
	}

	public SocketPolicyServer (string xml)
	{
		// transform the policy to a byte array (a single time)
		policy = Encoding.UTF8.GetBytes (xml);
	}

	public int Start ()
	{
		try {
			listen_socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			listen_socket.Bind (new IPEndPoint (IPAddress.Any, 843));
			listen_socket.Listen (500);
			listen_socket.Blocking = false;
		}
		catch (SocketException se) {
			// Most common mistake: port 843 is not user accessible on unix-like operating systems
			if (se.SocketErrorCode == SocketError.AccessDenied) {
				Console.WriteLine ("NOTE: must be run as root since the server listen to port 843");
				return 5;
			} else {
				Console.WriteLine (se);
				return 6;
			}
		}

		runner = new Thread (new ThreadStart (RunServer));
		runner.Start ();
		return 0;
	}

	void RunServer ()
	{
		accept_cb = new AsyncCallback (OnAccept);
		listen_socket.BeginAccept (accept_cb, null);

		while (true) // Just sleep until we're aborted.
			Thread.Sleep (1000000);
	}

	void OnAccept (IAsyncResult ar)
	{
		Console.WriteLine("incoming connection");
		Socket accepted = null;
		try {
			accepted = listen_socket.EndAccept (ar);
		} catch {
		} finally {
			listen_socket.BeginAccept (accept_cb, null);
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
				if (r.Buffer [i] != request [i]) {
					// invalid request, close socket
					socket.Close ();
					return;
				}
			}

			if (r.Length == request.Length) {
				Console.WriteLine("got policy request, sending response");
				// request complete, send policy
				socket.BeginSend (policy, 0, policy.Length, SocketFlags.None, new AsyncCallback (OnSend), socket);
			} else {
				// continue reading from socket
				socket.BeginReceive (r.Buffer, r.Length, request.Length - r.Length, SocketFlags.None, 
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
		runner.Abort ();
		listen_socket.Close ();
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
