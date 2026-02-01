$client = [System.Net.Sockets.TcpClient]::new("127.0.0.1", 42069)
$stream = $client.GetStream()
$writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
$writer.AutoFlush = $true
$writer.WriteLine("Do you have what it takes to be an engineer at TheStartup?")
$writer.Dispose()
$client.Close()