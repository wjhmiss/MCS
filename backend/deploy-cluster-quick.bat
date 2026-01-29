@echo off
setlocal enabledelayedexpansion

echo ========================================
echo MCS.Orleans 集群快速部署脚本
echo ========================================
echo.

if "%1"=="" (
    echo 用法: deploy-cluster-quick.bat [环境] [机器编号]
    echo.
    echo 环境:
    echo   dev  - 开发环境 (单机部署，使用 localhost)
    echo   prod - 生产环境 (多机部署，使用实际 IP)
    echo.
    echo 机器编号 (仅生产环境):
    echo   1 - 机器 1 (192.168.137.219) - 主节点 (Nginx + Silo#1 + PostgreSQL + Redis + API)
    echo   2 - 机器 2 (192.168.137.220) - 工作节点 (Silo#2 + API)
    echo   3 - 机器 3 (192.168.137.221) - 工作节点 (Silo#3 + API)
    echo.
    echo 示例:
    echo   deploy-cluster-quick.bat dev
    echo   deploy-cluster-quick.bat prod 1
    echo   deploy-cluster-quick.bat prod 2
    echo   deploy-cluster-quick.bat prod 3
    echo.
    pause
    exit /b 1
)

set ENV=%1

if "%ENV%"=="dev" (
    set COMPOSE_FILE=docker-compose.dev.yml
    set ENV_NAME=开发环境
    set MACHINE_NAME=本地开发
    goto :deploy
)

if "%ENV%"=="prod" (
    if "%2"=="" (
        echo 错误: 生产环境需要指定机器编号
        echo 用法: deploy-cluster-quick.bat prod [机器编号]
        echo 机器编号: 1, 2 或 3
        pause
        exit /b 1
    )
    
    set MACHINE_NUM=%2
    
    if "%MACHINE_NUM%"=="1" (
        set COMPOSE_FILE=docker-compose.machine1.yml
        set MACHINE_IP=192.168.137.219
        set MACHINE_NAME=机器 1 (主节点)
    ) else if "%MACHINE_NUM%"=="2" (
        set COMPOSE_FILE=docker-compose.machine2.yml
        set MACHINE_IP=192.168.137.220
        set MACHINE_NAME=机器 2 (工作节点)
    ) else if "%MACHINE_NUM%"=="3" (
        set COMPOSE_FILE=docker-compose.machine3.yml
        set MACHINE_IP=192.168.137.221
        set MACHINE_NAME=机器 3 (工作节点)
    ) else (
        echo 错误: 无效的机器编号 %MACHINE_NUM%
        echo 请使用 1, 2 或 3
        pause
        exit /b 1
    )
    
    set ENV_NAME=生产环境
    goto :deploy
)

echo 错误: 无效的环境 %ENV%
echo 请使用 dev 或 prod
pause
exit /b 1

:deploy
echo 部署配置:
echo   环境: %ENV_NAME%
echo   机器名称: %MACHINE_NAME%
if defined MACHINE_IP (
    echo   机器 IP: %MACHINE_IP%
)
echo   配置文件: %COMPOSE_FILE%
echo.

echo [1/6] 检查 Docker 是否安装...
docker --version >nul 2>&1
if errorlevel 1 (
    echo 错误: Docker 未安装或未运行
    pause
    exit /b 1
)
echo Docker 已安装
echo.

echo [2/6] 检查 Docker Compose 是否安装...
docker-compose --version >nul 2>&1
if errorlevel 1 (
    echo 错误: Docker Compose 未安装或未运行
    pause
    exit /b 1
)
echo Docker Compose 已安装
echo.

echo [3/6] 检查配置文件是否存在...
if not exist "%COMPOSE_FILE%" (
    echo 错误: 配置文件 %COMPOSE_FILE% 不存在
    pause
    exit /b 1
)
echo 配置文件存在
echo.

echo [4/6] 检查项目文件...
if not exist "MCS.API\MCS.API.csproj" (
    echo 错误: MCS.API 项目文件不存在
    pause
    exit /b 1
)
if not exist "MCS.Silo\MCS.Silo.csproj" (
    echo 错误: MCS.Silo 项目文件不存在
    pause
    exit /b 1
)
if not exist "MCS.Grains\MCS.Grains.csproj" (
    echo 错误: MCS.Grains 项目文件不存在
    pause
    exit /b 1
)
echo 项目文件完整
echo.

echo [5/6] 检查配置文件...
if "%ENV%"=="prod" (
    if not exist "MCS.API\appsettings.Production.json" (
        echo 错误: API 生产环境配置文件不存在
        pause
        exit /b 1
    )
    if not exist "MCS.Silo\appsettings.Production.json" (
        echo 错误: Silo 生产环境配置文件不存在
        pause
        exit /b 1
    )
    echo 生产环境配置文件存在
) else (
    if not exist "MCS.API\appsettings.Development.json" (
        echo 错误: API 开发环境配置文件不存在
        pause
        exit /b 1
    )
    if not exist "MCS.Silo\appsettings.Development.json" (
        echo 错误: Silo 开发环境配置文件不存在
        pause
        exit /b 1
    )
    echo 开发环境配置文件存在
)
echo.

echo [6/6] 停止现有容器...
docker-compose -f %COMPOSE_FILE% down 2>nul
echo.

echo [7/7] 启动服务...
docker-compose -f %COMPOSE_FILE% up -d --build
if errorlevel 1 (
    echo 错误: 服务启动失败
    pause
    exit /b 1
)
echo 服务启动成功
echo.

echo ========================================
echo 部署完成！
echo ========================================
echo.
echo 服务状态:
docker-compose -f %COMPOSE_FILE% ps
echo.

echo 管理命令:
echo   查看日志: docker-compose -f %COMPOSE_FILE% logs -f
echo   停止服务: docker-compose -f %COMPOSE_FILE% down
echo   重启服务: docker-compose -f %COMPOSE_FILE% restart
echo   查看状态: docker-compose -f %COMPOSE_FILE% ps
echo.

if "%ENV%"=="dev" (
    echo 开发环境访问地址:
    echo   API: http://localhost:5000
    echo   Silo Gateway: http://localhost:30000
    echo   PostgreSQL: localhost:5432
    echo   Redis: localhost:6379
    echo.
    echo API 测试命令:
    echo   curl -X POST http://localhost:5000/api/workflow/serial -H "Content-Type: application/json" -d "{\"name\":\"测试\",\"taskNames\":[\"任务1\",\"任务2\"]}"
    echo.
) else (
    echo 生产环境访问地址:
    if "%MACHINE_NUM%"=="1" (
        echo   Nginx 负载均衡器: http://%MACHINE_IP%
        echo   Nginx 负载均衡器 (HTTPS): https://%MACHINE_IP%
        echo   API: http://%MACHINE_IP%:5000
        echo   Silo Gateway: http://%MACHINE_IP%:30000
        echo   PostgreSQL: %MACHINE_IP%:5432
        echo   Redis: %MACHINE_IP%:6379
        echo.
        echo Nginx 负载均衡器测试:
        echo   curl http://%MACHINE_IP%/health
        echo   curl http://%MACHINE_IP%/api/workflow/serial -X POST -H "Content-Type: application/json" -d "{\"name\":\"测试\",\"taskNames\":[\"任务1\",\"任务2\"]}"
        echo.
    ) else (
        echo   API: http://%MACHINE_IP%:5000
        echo   Silo Gateway: http://%MACHINE_IP%:30000
        echo.
        echo API 测试命令:
        echo   curl http://%MACHINE_IP%:5000/health
        echo   curl http://%MACHINE_IP%:5000/api/workflow/serial -X POST -H "Content-Type: application/json" -d "{\"name\":\"测试\",\"taskNames\":[\"任务1\",\"任务2\"]}"
        echo.
    )
)

pause
