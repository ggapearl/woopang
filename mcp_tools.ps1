# WOOPANG MCP Tools
# Usage: powershell -ExecutionPolicy Bypass -File mcp_tools.ps1 -Action <action> [-Param1 value1] [-Param2 value2]

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("status", "play", "stop", "hierarchy", "addObject", "saveScene", "getObject", "loadScene")]
    [string]$Action,

    [string]$ObjectName = "",
    [string]$ParentPath = "",
    [string]$ScenePath = ""
)

function Send-McpCommand {
    param(
        [string]$Method,
        [hashtable]$Params = @{}
    )

    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $uri = [System.Uri]::new("ws://localhost:8090/McpUnity")
    $cts = New-Object System.Threading.CancellationTokenSource
    $cts.CancelAfter(10000)

    try {
        $ws.ConnectAsync($uri, $cts.Token).Wait()

        $message = @{
            jsonrpc = "2.0"
            id = [System.Guid]::NewGuid().ToString()
            method = $Method
            params = $Params
        } | ConvertTo-Json -Compress -Depth 10

        Write-Host "Sending: $Method" -ForegroundColor Cyan
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($message)
        $segment = New-Object System.ArraySegment[byte] -ArgumentList @(,$bytes)
        $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait()

        Start-Sleep -Milliseconds 500
        $buffer = New-Object byte[] 65536
        $receiveSegment = New-Object System.ArraySegment[byte] -ArgumentList @(,$buffer)
        $result = $ws.ReceiveAsync($receiveSegment, $cts.Token).Result
        $response = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)

        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Done", $cts.Token).Wait()

        return $response | ConvertFrom-Json
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
        return $null
    }
}

switch ($Action) {
    "status" {
        Write-Host "=== Unity Status ===" -ForegroundColor Green
        $result = Send-McpCommand -Method "execute_menu_item" -Params @{ menuPath = "WOOPANG/MCP/Log Status" }
        if ($result) {
            Write-Host "Response: $($result | ConvertTo-Json -Depth 5)"
        }
    }

    "play" {
        Write-Host "=== Starting Play Mode ===" -ForegroundColor Green
        $result = Send-McpCommand -Method "execute_menu_item" -Params @{ menuPath = "WOOPANG/MCP/Start Play Mode" }
        if ($result) { Write-Host "Play mode started!" -ForegroundColor Green }
    }

    "stop" {
        Write-Host "=== Stopping Play Mode ===" -ForegroundColor Green
        $result = Send-McpCommand -Method "execute_menu_item" -Params @{ menuPath = "WOOPANG/MCP/Stop Play Mode" }
        if ($result) { Write-Host "Play mode stopped!" -ForegroundColor Green }
    }

    "hierarchy" {
        Write-Host "=== Scene Hierarchy ===" -ForegroundColor Green
        $result = Send-McpCommand -Method "get_scenes_hierarchy" -Params @{}
        if ($result -and $result.result) {
            $result.result | ConvertTo-Json -Depth 10
        }
    }

    "addObject" {
        if ([string]::IsNullOrEmpty($ObjectName)) {
            Write-Host "Please provide -ObjectName" -ForegroundColor Red
            exit 1
        }
        Write-Host "=== Adding GameObject: $ObjectName ===" -ForegroundColor Green
        $params = @{
            name = $ObjectName
        }
        if (-not [string]::IsNullOrEmpty($ParentPath)) {
            $params["parentPath"] = $ParentPath
        }
        $result = Send-McpCommand -Method "update_gameobject" -Params $params
        if ($result) {
            Write-Host "GameObject added: $($result | ConvertTo-Json -Depth 5)"
        }
    }

    "getObject" {
        if ([string]::IsNullOrEmpty($ObjectName)) {
            Write-Host "Please provide -ObjectName" -ForegroundColor Red
            exit 1
        }
        Write-Host "=== Getting GameObject: $ObjectName ===" -ForegroundColor Green
        $result = Send-McpCommand -Method "get_gameobject" -Params @{ objectPath = $ObjectName }
        if ($result -and $result.result) {
            $result.result | ConvertTo-Json -Depth 10
        }
    }

    "saveScene" {
        Write-Host "=== Saving Scene ===" -ForegroundColor Green
        $result = Send-McpCommand -Method "save_scene" -Params @{}
        if ($result) { Write-Host "Scene saved!" -ForegroundColor Green }
    }

    "loadScene" {
        if ([string]::IsNullOrEmpty($ScenePath)) {
            Write-Host "Please provide -ScenePath" -ForegroundColor Red
            exit 1
        }
        Write-Host "=== Loading Scene: $ScenePath ===" -ForegroundColor Green
        $result = Send-McpCommand -Method "load_scene" -Params @{ scenePath = $ScenePath }
        if ($result) {
            Write-Host "Scene loaded: $($result | ConvertTo-Json -Depth 5)"
        }
    }
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
