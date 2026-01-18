# Wait a bit for Unity to settle
Start-Sleep -Seconds 2

$ws = New-Object System.Net.WebSockets.ClientWebSocket
$uri = [System.Uri]::new("ws://localhost:8090/McpUnity")
$cts = New-Object System.Threading.CancellationTokenSource
$cts.CancelAfter(5000)

try {
    $ws.ConnectAsync($uri, $cts.Token).Wait()
    Write-Host "MCP Server connected!"

    # Check status via Log Status menu
    $message = @{
        jsonrpc = "2.0"
        id = 1
        method = "execute_menu_item"
        params = @{
            menuPath = "WOOPANG/MCP/Log Status"
        }
    } | ConvertTo-Json -Compress

    Write-Host "Checking Unity status..."
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($message)
    $segment = New-Object System.ArraySegment[byte] -ArgumentList @(,$bytes)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait()

    Start-Sleep -Milliseconds 500
    $buffer = New-Object byte[] 4096
    $receiveSegment = New-Object System.ArraySegment[byte] -ArgumentList @(,$buffer)
    $result = $ws.ReceiveAsync($receiveSegment, $cts.Token).Result
    $response = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
    Write-Host "Response: $response"

    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", $cts.Token).Wait()
} catch {
    Write-Host "Cannot connect to MCP Server."
    Write-Host "Unity is likely in Play mode (MCP server stops during Play mode unless Fast Play Mode is enabled)"
    Write-Host "Error: $_"
}
