use std::sync::Mutex;

use tauri::{Manager, RunEvent};
use tauri_plugin_shell::process::{CommandChild, CommandEvent};
use tauri_plugin_shell::ShellExt;

/// The running .NET API sidecar, killed when the app exits.
struct ApiProcess(Mutex<Option<CommandChild>>);

fn start_api(app: &tauri::App) -> Result<(), Box<dyn std::error::Error>> {
    let data_dir = app.path().app_data_dir()?;
    std::fs::create_dir_all(&data_dir)?;
    let database_path = data_dir.join("pharmaai.db");

    let (mut events, child) = app
        .shell()
        .sidecar("pharmaai-api")?
        .args([format!("--Database:Path={}", database_path.display())])
        .spawn()?;

    tauri::async_runtime::spawn(async move {
        while let Some(event) = events.recv().await {
            match event {
                CommandEvent::Stdout(line) => log::info!("[api] {}", String::from_utf8_lossy(&line)),
                CommandEvent::Stderr(line) => log::warn!("[api] {}", String::from_utf8_lossy(&line)),
                CommandEvent::Terminated(status) => log::error!("[api] exited: {:?}", status.code),
                _ => {}
            }
        }
    });

    app.manage(ApiProcess(Mutex::new(Some(child))));
    Ok(())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| start_api(app))
        .build(tauri::generate_context!())
        .expect("failed to build PharmaAI desktop app")
        .run(|app, event| {
            if let RunEvent::Exit = event {
                if let Some(child) = app.state::<ApiProcess>().0.lock().unwrap().take() {
                    let _ = child.kill();
                }
            }
        });
}
