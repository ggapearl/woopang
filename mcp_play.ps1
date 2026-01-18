$ws = New-Object System.Net.WebSockets.ClientWebSocket
$uri = [System.Uri]::new("ws://localhost:8090/McpUnity")
$cts = New-Object System.Threading.CancellationTokenSource
$cts.CancelAfter(10000)

try {
    Write-Host "Connecting to MCP Unity Server..."
    $ws.ConnectAsync($uri, $cts.Token).Wait()
    Write-Host "Connected!"

    # MCP protocol message for execute_menu_item (JSON-RPC style)
    $message = @{
        jsonrpc = "2.0"
        id = 1
        method = "execute_menu_item"
        params = @{
            menuPath = "WOOPANG/MCP/Start Play Mode"
        }
    } | ConvertTo-Json -Compress

    Write-Host "Sending: $message"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($message)
    $segment = New-Object System.ArraySegment[byte] -ArgumentList @(,$bytes)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait()
    Write-Host "Command sent! Waiting for Unity response..."

    # Receive response - Unity executes async so may take a moment
    Start-Sleep -Milliseconds 500
    $buffer = New-Object byte[] 8192
    $receiveSegment = New-Object System.ArraySegment[byte] -ArgumentList @(,$buffer)

    try {
        $result = $ws.ReceiveAsync($receiveSegment, $cts.Token).Result
        $response = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
        Write-Host "Response: $response"
    } catch {
        Write-Host "Note: Command was sent. Check Unity for result."
    }

    Start-Sleep -Milliseconds 300
    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", $cts.Token).Wait()
    Write-Host "Connection closed."
} catch {
    Write-Host "Error: $_"
}
