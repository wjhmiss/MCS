@echo off
echo Starting MCS Task Management System...

docker-compose up -d

echo.
echo Services are starting...
echo.
echo Frontend: http://localhost:3000
echo Backend API: http://localhost:5000
echo Swagger UI: http://localhost:5000/swagger
echo.
echo Press Ctrl+C to stop all services
echo.

docker-compose logs -f
