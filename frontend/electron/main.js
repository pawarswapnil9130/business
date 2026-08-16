const { app, BrowserWindow } = require('electron');
const path = require('path');
const http = require('http');
const { spawn } = require('child_process');

let mainWindow = null;
let backendProcess = null;

const BACKEND_URL = 'http://localhost:5000';
const BACKEND_HEALTH = 'http://localhost:5000/swagger/v1/swagger.json';

function checkBackendReady(url, maxAttempts = 30, interval = 500) {
  return new Promise((resolve, reject) => {
    let attempts = 0;
    const check = () => {
      attempts++;
      http.get(url, (res) => {
        if (res.statusCode >= 200 && res.statusCode < 400) {
          resolve(true);
        } else {
          retry();
        }
      }).on('error', () => {
        retry();
      });
    };

    const retry = () => {
      if (attempts >= maxAttempts) {
        // Resolve anyway to try loading UI
        resolve(false);
      } else {
        setTimeout(check, interval);
      }
    };

    check();
  });
}

function startBackend() {
  const isDev = process.env.NODE_ENV === 'development';
  if (isDev) {
    console.log('[Desktop Launcher] Development mode: assuming backend is started manually or via npm.');
    return;
  }

  const backendDll = path.join(__dirname, '..', '..', 'backend', 'ApparelERP.Api', 'bin', 'Release', 'net9.0', 'ApparelERP.Api.dll');
  const backendExe = path.join(__dirname, '..', '..', 'backend', 'ApparelERP.Api', 'bin', 'Release', 'net9.0', 'ApparelERP.Api.exe');
  const projectDir = path.join(__dirname, '..', '..', 'backend', 'ApparelERP.Api');

  try {
    const fs = require('fs');
    if (fs.existsSync(backendExe)) {
      console.log('[Desktop Launcher] Starting backend executable:', backendExe);
      backendProcess = spawn(backendExe, [], {
        cwd: path.dirname(backendExe),
        env: { ...process.env, ASPNETCORE_URLS: 'http://localhost:5000' },
        stdio: 'ignore'
      });
    } else {
      console.log('[Desktop Launcher] Starting backend via dotnet run from:', projectDir);
      backendProcess = spawn('dotnet', ['run', '--project', projectDir, '--urls', 'http://localhost:5000'], {
        cwd: projectDir,
        stdio: 'ignore'
      });
    }
  } catch (err) {
    console.error('[Desktop Launcher] Failed to spawn backend process:', err);
  }
}

async function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1366,
    height: 850,
    minWidth: 1024,
    minHeight: 700,
    title: 'Apparel ERP - Manufacturing & Trading Management',
    backgroundColor: '#0f172a',
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true
    },
    autoHideMenuBar: true
  });

  console.log('[Desktop Launcher] Waiting for backend server...');
  await checkBackendReady(BACKEND_HEALTH);

  const targetUrl = process.env.ELECTRON_START_URL || BACKEND_URL;
  console.log(`[Desktop Launcher] Loading URL: ${targetUrl}`);
  
  mainWindow.loadURL(targetUrl);

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

app.whenReady().then(() => {
  startBackend();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (backendProcess) {
    try {
      console.log('[Desktop Launcher] Shutting down backend process...');
      backendProcess.kill();
    } catch (e) {
      console.error(e);
    }
  }
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
