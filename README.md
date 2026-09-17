# WorkerRetryOperationsASP

Worker (.NET 9) que reintenta inserts de webhooks STP/Itravel que fallaron por
timeout de SQL Server. Lee el log diario del middleware
(`NotifyItravel_STP.log_{yyyy-MM-dd}.log`), detecta las líneas de error
`Error ProcessSTPWebhook - EventType: ... - RequestId: ...`, reconstruye el
payload original correlacionando por `cveRastreo` con la línea `[SUCCESS]`
correspondiente en el mismo archivo, y vuelve a llamar al stored procedure
`dbo.usp_WebhookItravel_InsertASP`. Si el reintento tiene éxito, se registra en
la tabla `dbo.reintentos_asp` para no volver a reintentarlo en corridas
futuras.

Solo se procesan errores de tipo `ProcessSTPWebhook` (los que coinciden con el
SP conocido). Los errores `InsertCreateOrderASP_SPAsync` (sin identificador
recuperable en el log, otro SP) quedan fuera de alcance.

## Estructura

- `src/WorkerRetryOperationsASP/`: código del worker.
- `sql/001_create_reintentos_asp.sql`: script de creación de la tabla de control.
- `logs-ejemplo/`: copia de logs de referencia usados para diseñar el parser (no se compilan, no son parte del proyecto).

## Configuración

Editar `src/WorkerRetryOperationsASP/appsettings.Production.json` (no se
versiona con valores reales) o definir variables de entorno equivalentes en el
servidor:

- `ConnectionStrings__AspDb`: cadena de conexión a la base de datos SQL Server.
- `LogSource__FolderPath`: carpeta donde el middleware escribe los logs diarios.
- `RetryWorker__IntervalHours`: intervalo de ejecución en horas (default `1`).

## Preparar la base de datos

Ejecutar `sql/001_create_reintentos_asp.sql` contra la base de datos destino
antes de desplegar el worker.

## Compilar y correr en desarrollo

```bash
dotnet build
dotnet run --project src/WorkerRetryOperationsASP
```

En desarrollo (`appsettings.Development.json`) el worker lee de la carpeta
`logs-ejemplo/` en la raíz del repo (ruta relativa al proyecto, funciona sin
importar desde dónde se invoque `dotnet run`).

## Modo dry-run (sin tocar SQL Server)

Para ver qué `cve_rastreo` se reintentarían y con qué parámetros exactos se
llamaría al SP (`@RequestId`, `@EventType`, `@Payload`, `@Status`), sin
ejecutar ningún insert real y **sin necesitar `ConnectionStrings:AspDb`
configurada**:

```bash
dotnet run --project src/WorkerRetryOperationsASP -- --RetryWorker:DryRun=true
```

El proceso parsea el log del día, correlaciona los candidatos y termina solo
(no entra al loop de 1 hora). Sirve para validar el parseo/correlación antes
de conectar contra una base de datos real.

## Desplegar como Servicio de Windows

1. Publicar:
   ```powershell
   dotnet publish src/WorkerRetryOperationsASP/WorkerRetryOperationsASP.csproj -c Release -r win-x64 --self-contained false -o C:\Services\WorkerRetryOperationsASP
   ```
2. Colocar `appsettings.Production.json` (o configurar las variables de
   entorno del punto anterior) junto al ejecutable publicado.
3. Crear el servicio (nota: el espacio después de `binPath=`, `start=` y
   `DisplayName=` es obligatorio, es una particularidad de `sc.exe`):
   ```powershell
   sc.exe create WorkerRetryOperationsASP binPath= "C:\Services\WorkerRetryOperationsASP\WorkerRetryOperationsASP.exe" start= auto DisplayName= "Worker Retry Operations ASP"
   ```
4. Configurar reinicio automático ante fallos:
   ```powershell
   sc.exe failure WorkerRetryOperationsASP reset= 86400 actions= restart/60000/restart/60000/restart/60000
   ```
5. Otorgar a la cuenta con la que corre el servicio: permiso de lectura sobre
   la carpeta de logs, y permisos SQL para ejecutar
   `dbo.usp_WebhookItravel_InsertASP` y hacer `SELECT`/`INSERT` sobre
   `dbo.reintentos_asp`.
6. Iniciar el servicio:
   ```powershell
   sc.exe start WorkerRetryOperationsASP
   ```
7. Verificar en el Visor de Eventos de Windows (Application, origen
   `WorkerRetryOperationsASP`) el log de arranque y el resumen de la primera
   corrida.

Para actualizar una versión ya instalada: `sc.exe stop`, reemplazar los
binarios publicados, `sc.exe start`. Usar `sc.exe delete` solo si se va a
recrear el servicio desde cero.
