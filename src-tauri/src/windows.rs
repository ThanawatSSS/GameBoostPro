use crate::model::{Platform, ProcessIdentity};
use std::{
    ffi::c_void,
    mem::{size_of, zeroed},
    os::windows::process::CommandExt,
    path::{Path, PathBuf},
    process::{Command, Output},
};
use windows_sys::Win32::{
    Foundation::*,
    Graphics::Gdi::*,
    Security::{Authorization::*, *},
    System::{Diagnostics::ToolHelp::*, Power::*, SystemInformation::*, Threading::*},
    UI::{Shell::*, WindowsAndMessaging::*},
};
use winreg::{enums::*, RegKey};

pub fn wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(Some(0)).collect()
}
pub fn string(s: &[u16]) -> String {
    String::from_utf16_lossy(&s[..s.iter().position(|x| *x == 0).unwrap_or(s.len())])
}
pub struct Handle(pub HANDLE);
impl Drop for Handle {
    fn drop(&mut self) {
        if !self.0.is_null() && self.0 != INVALID_HANDLE_VALUE {
            unsafe {
                CloseHandle(self.0);
            }
        }
    }
}
pub fn admin() -> bool {
    unsafe { IsUserAnAdmin() != 0 }
}
pub struct Instance {
    _handles: Vec<Handle>,
}
pub fn instance() -> Result<Instance, String> {
    unsafe {
        let mut handles = Vec::new();
        let names = if admin() {
            vec![
                r"Local\Codex.GameBoostPro",
                r"Global\Codex.GameBoostPro.RustV4",
            ]
        } else {
            vec![r"Local\Codex.GameBoostPro"]
        };
        for name in names {
            let h = Handle(CreateMutexW(std::ptr::null(), 1, wide(name).as_ptr()));
            if h.0.is_null() {
                return Err("Cannot establish a single application instance".into());
            }
            if GetLastError() == ERROR_ALREADY_EXISTS {
                let result = WaitForSingleObject(h.0, 7000);
                if result != WAIT_OBJECT_0 && result != WAIT_ABANDONED {
                    return Err("Game Boost Pro is already running. Close the existing window or tray app first.".into());
                }
            }
            handles.push(h);
        }
        Ok(Instance { _handles: handles })
    }
}
pub fn owner() -> Result<String, String> {
    unsafe {
        let mut h = std::ptr::null_mut();
        if OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &mut h) == 0 {
            return Err("Cannot identify Windows account".into());
        }
        let token = Handle(h);
        token_owner(token.0)
    }
}
unsafe fn token_owner(token: HANDLE) -> Result<String, String> {
    unsafe {
        let mut needed = 0;
        GetTokenInformation(token, TokenUser, std::ptr::null_mut(), 0, &mut needed);
        if needed == 0 || needed > 65536 {
            return Err("Invalid account token".into());
        }
        let mut buffer = vec![0usize; (needed as usize).div_ceil(size_of::<usize>())];
        if GetTokenInformation(
            token,
            TokenUser,
            buffer.as_mut_ptr().cast(),
            needed,
            &mut needed,
        ) == 0
        {
            return Err("Cannot read account token".into());
        }
        let user = &*(buffer.as_ptr().cast::<TOKEN_USER>());
        let mut text = std::ptr::null_mut();
        if ConvertSidToStringSidW(user.User.Sid, &mut text) == 0 {
            return Err("Cannot read account SID".into());
        }
        let mut len = 0;
        while *text.add(len) != 0 && len < 256 {
            len += 1;
        }
        let value = String::from_utf16_lossy(std::slice::from_raw_parts(text, len));
        LocalFree(text.cast());
        Ok(value)
    }
}
pub fn data_dir() -> Result<PathBuf, String> {
    let base = std::env::var_os("LOCALAPPDATA").ok_or("Local application data is unavailable")?;
    let path = PathBuf::from(base).join("CodexGameBoost");
    as_standard_user(|| std::fs::create_dir_all(&path).map_err(|e| e.to_string()))?;
    Ok(path)
}
pub fn identity(pid: u32) -> Option<ProcessIdentity> {
    unsafe {
        let handle = Handle(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, pid));
        if handle.0.is_null() {
            return None;
        }
        let mut name = vec![0u16; 32768];
        let mut length = name.len() as u32;
        if QueryFullProcessImageNameW(handle.0, 0, name.as_mut_ptr(), &mut length) == 0 {
            return None;
        }
        let path = String::from_utf16_lossy(&name[..length as usize]);
        let mut created = zeroed();
        let mut exited = zeroed();
        let mut kernel = zeroed();
        let mut user = zeroed();
        if GetProcessTimes(handle.0, &mut created, &mut exited, &mut kernel, &mut user) == 0 {
            return None;
        }
        Some(ProcessIdentity {
            pid,
            started: ft(created),
            name: Path::new(&path).file_name()?.to_string_lossy().into(),
            path,
        })
    }
}
pub fn same(a: &ProcessIdentity) -> bool {
    identity(a.pid).is_some_and(|p| p.started == a.started && p.path.eq_ignore_ascii_case(&a.path))
}
pub fn processes() -> Vec<(u32, String)> {
    unsafe {
        let h = Handle(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0));
        if h.0 == INVALID_HANDLE_VALUE {
            return vec![];
        }
        let mut entry: PROCESSENTRY32W = zeroed();
        entry.dwSize = size_of::<PROCESSENTRY32W>() as u32;
        let mut output = vec![];
        let mut ok = Process32FirstW(h.0, &mut entry);
        while ok != 0 {
            output.push((entry.th32ProcessID, string(&entry.szExeFile)));
            ok = Process32NextW(h.0, &mut entry);
        }
        output
    }
}
pub fn priority(target: &ProcessIdentity, set: Option<u32>) -> Result<u32, String> {
    if crate::model::protected(&target.name) || !same(target) {
        return Err("The game process changed or is protected".into());
    }
    unsafe {
        let rights = PROCESS_QUERY_LIMITED_INFORMATION
            | if set.is_some() {
                PROCESS_SET_INFORMATION
            } else {
                0
            };
        let h = Handle(OpenProcess(rights, 0, target.pid));
        if h.0.is_null() {
            return Err("Game/anti-cheat denied process access".into());
        }
        let mut created = zeroed();
        let mut exited = zeroed();
        let mut kernel = zeroed();
        let mut user = zeroed();
        let mut path = vec![0u16; 32768];
        let mut len = path.len() as u32;
        if GetProcessTimes(h.0, &mut created, &mut exited, &mut kernel, &mut user) == 0
            || ft(created) != target.started
            || QueryFullProcessImageNameW(h.0, 0, path.as_mut_ptr(), &mut len) == 0
            || !String::from_utf16_lossy(&path[..len as usize]).eq_ignore_ascii_case(&target.path)
        {
            return Err("The game process identity changed".into());
        }
        let before = GetPriorityClass(h.0);
        if before == 0 {
            return Err("Cannot read game priority".into());
        }
        if let Some(p) = set {
            if ![
                NORMAL_PRIORITY_CLASS,
                BELOW_NORMAL_PRIORITY_CLASS,
                IDLE_PRIORITY_CLASS,
                ABOVE_NORMAL_PRIORITY_CLASS,
                HIGH_PRIORITY_CLASS,
            ]
            .contains(&p)
            {
                return Err("Invalid priority recovery value".into());
            }
            if SetPriorityClass(h.0, p) == 0 || GetPriorityClass(h.0) != p {
                return Err("Game/anti-cheat rejected priority change".into());
            }
        }
        Ok(before)
    }
}
pub fn system_tool(name: &str) -> PathBuf {
    let mut buffer = vec![0u16; 32768];
    let length = unsafe { GetSystemDirectoryW(buffer.as_mut_ptr(), buffer.len() as u32) };
    PathBuf::from(String::from_utf16_lossy(
        &buffer[..length.min(32767) as usize],
    ))
    .join(name)
}
pub fn command(path: &Path) -> Command {
    let mut c = Command::new(path);
    c.creation_flags(0x08000000);
    c
}
pub fn powercfg(args: &[&str]) -> Result<Output, String> {
    let mut child = command(&system_tool("powercfg.exe"))
        .args(args)
        .stdin(std::process::Stdio::null())
        .stdout(std::process::Stdio::piped())
        .stderr(std::process::Stdio::piped())
        .spawn()
        .map_err(|e| e.to_string())?;
    use std::io::Read;
    let stdout = child.stdout.take().ok_or("Missing power command output")?;
    let stderr = child.stderr.take().ok_or("Missing power command errors")?;
    let out = std::thread::spawn(move || {
        let mut data = vec![];
        let _ = stdout.take(65536).read_to_end(&mut data);
        data
    });
    let err = std::thread::spawn(move || {
        let mut data = vec![];
        let _ = stderr.take(65536).read_to_end(&mut data);
        data
    });
    let start = std::time::Instant::now();
    let status = loop {
        match child.try_wait() {
            Ok(Some(status)) => break status,
            Ok(None) => (),
            Err(e) => {
                let _ = child.kill();
                let _ = child.wait();
                let _ = out.join();
                let _ = err.join();
                return Err(e.to_string());
            }
        }
        if start.elapsed().as_secs() > 15 {
            let _ = child.kill();
            let _ = child.wait();
            let _ = out.join();
            let _ = err.join();
            return Err("Power settings timed out".into());
        }
        std::thread::sleep(std::time::Duration::from_millis(25));
    };
    let output = Output {
        status,
        stdout: out.join().map_err(|_| "Power command output failed")?,
        stderr: err.join().map_err(|_| "Power command errors failed")?,
    };
    if !output.status.success() {
        return Err("Windows rejected the power plan command".into());
    }
    Ok(output)
}
#[repr(C)]
struct Guid {
    a: u32,
    b: u16,
    c: u16,
    d: [u8; 8],
}
#[link(name = "powrprof")]
unsafe extern "system" {
    fn PowerGetActiveScheme(root: *mut c_void, scheme: *mut *mut Guid) -> u32;
    fn PowerSetActiveScheme(root: *mut c_void, scheme: *const Guid) -> u32;
    fn PowerReadFriendlyName(
        root: *mut c_void,
        scheme: *const Guid,
        subgroup: *const Guid,
        setting: *const Guid,
        buffer: *mut u8,
        size: *mut u32,
    ) -> u32;
}
pub fn power() -> Result<(String, String), String> {
    unsafe {
        let mut ptr = std::ptr::null_mut();
        if PowerGetActiveScheme(std::ptr::null_mut(), &mut ptr) != 0 || ptr.is_null() {
            return Err("Cannot read active power plan".into());
        }
        let g = &*ptr;
        let u = uuid::Uuid::from_fields(g.a, g.b, g.c, &g.d);
        let mut size = 0;
        PowerReadFriendlyName(
            std::ptr::null_mut(),
            ptr,
            std::ptr::null(),
            std::ptr::null(),
            std::ptr::null_mut(),
            &mut size,
        );
        let mut name = "Current plan".into();
        if size > 0 && size <= 65536 {
            let mut b = vec![0u16; (size as usize).div_ceil(2)];
            if PowerReadFriendlyName(
                std::ptr::null_mut(),
                ptr,
                std::ptr::null(),
                std::ptr::null(),
                b.as_mut_ptr().cast(),
                &mut size,
            ) == 0
            {
                name = string(&b);
            }
        }
        LocalFree(ptr.cast());
        Ok((u.to_string(), name))
    }
}
pub fn set_power(value: &str) -> Result<(), String> {
    let u = uuid::Uuid::parse_str(value).map_err(|_| "Invalid power plan identifier")?;
    let (a, b, c, d) = u.as_fields();
    let g = Guid { a, b, c, d: *d };
    unsafe {
        if PowerSetActiveScheme(std::ptr::null_mut(), &g) != 0 {
            return Err("Cannot activate power plan".into());
        }
    }
    if power()?.0 != u.to_string() {
        return Err("Power plan was overridden by another program".into());
    }
    Ok(())
}
pub fn platform() -> Platform {
    let bios = RegKey::predef(HKEY_LOCAL_MACHINE)
        .open_subkey(r"HARDWARE\DESCRIPTION\System\BIOS")
        .ok();
    let read = |name| {
        bios.as_ref()
            .and_then(|k| k.get_value::<String, _>(name).ok())
            .unwrap_or_default()
    };
    let manufacturer = read("SystemManufacturer");
    let model = read("SystemProductName");
    let nitro_sense = RegKey::predef(HKEY_LOCAL_MACHINE)
        .open_subkey(r"SYSTEM\CurrentControlSet\Services\AASSvc")
        .is_ok();
    let mut caps = unsafe { zeroed::<SYSTEM_POWER_CAPABILITIES>() };
    let valid = unsafe { GetPwrCapabilities(&mut caps) };
    let laptop = !valid || caps.LidPresent || caps.SystemBatteriesPresent;
    let mut gpu = vec![];
    unsafe {
        for i in 0..16 {
            let mut device: DISPLAY_DEVICEW = zeroed();
            device.cb = size_of::<DISPLAY_DEVICEW>() as u32;
            if EnumDisplayDevicesW(std::ptr::null(), i, &mut device, 0) == 0 {
                break;
            }
            if device.StateFlags & DISPLAY_DEVICE_ACTIVE != 0 {
                gpu.push(string(&device.DeviceString));
            }
        }
    }
    let mut status = unsafe { zeroed::<SYSTEM_POWER_STATUS>() };
    unsafe {
        GetSystemPowerStatus(&mut status);
    }
    let supported = !laptop || (manufacturer.to_lowercase().contains("acer") && nitro_sense);
    Platform {
        manufacturer,
        model,
        laptop,
        nitro_sense,
        supported,
        gpu,
        power_name: power().map(|p| p.1).unwrap_or_default(),
        ac: status.ACLineStatus == 1,
    }
}
fn ft(t: FILETIME) -> u64 {
    ((t.dwHighDateTime as u64) << 32) | t.dwLowDateTime as u64
}
pub fn metrics(previous: &mut Option<(u64, u64)>) -> (f64, u32, f64, f64) {
    unsafe {
        let mut idle = zeroed();
        let mut kernel = zeroed();
        let mut user = zeroed();
        let mut cpu = 0.0;
        if GetSystemTimes(&mut idle, &mut kernel, &mut user) != 0 {
            let total = ft(kernel) + ft(user);
            let id = ft(idle);
            if let Some((pt, pi)) = *previous {
                let dt = total.saturating_sub(pt);
                if dt > 0 {
                    cpu = (100.0 * (1.0 - id.saturating_sub(pi) as f64 / dt as f64))
                        .clamp(0.0, 100.0);
                }
            }
            *previous = Some((total, id));
        }
        let mut mem: MEMORYSTATUSEX = zeroed();
        mem.dwLength = size_of::<MEMORYSTATUSEX>() as u32;
        GlobalMemoryStatusEx(&mut mem);
        let gb = 1073741824.0;
        (
            cpu,
            mem.dwMemoryLoad,
            (mem.ullTotalPhys - mem.ullAvailPhys) as f64 / gb,
            mem.ullTotalPhys as f64 / gb,
        )
    }
}
pub fn open_target(which: &str) -> Result<(), String> {
    let target = match which {
        "graphics" => "ms-settings:display-advancedgraphics",
        "nvidia" => "nvidia",
        _ => return Err("Unknown destination".into()),
    };
    if target == "nvidia" {
        for p in [
            r"C:\Program Files\NVIDIA Corporation\Control Panel Client\nvcplui.exe",
            r"C:\Program Files\WindowsApps",
        ] {
            if p.ends_with(".exe") && Path::new(p).is_file() {
                return launch_game(p);
            }
        }
        // The Store control panel registers this fixed shell application identity.
        launch_game("shell:AppsFolder\\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel")?;
        return Ok(());
    }
    launch_game(target)
}
pub fn launch_game(target: &str) -> Result<(), String> {
    if !admin() {
        return shell(target, None);
    }
    let primary = standard_token()?;
    unsafe {
        let (application, args) = if Path::new(target).is_absolute() {
            (PathBuf::from(target), format!("\"{target}\""))
        } else {
            let exe = system_tool("rundll32.exe");
            let args = format!("\"{}\" url.dll,FileProtocolHandler {target}", exe.display());
            (exe, args)
        };
        let app = wide(&application.to_string_lossy());
        let mut args = wide(&args);
        let mut startup: STARTUPINFOW = zeroed();
        startup.cb = size_of::<STARTUPINFOW>() as u32;
        let mut info: PROCESS_INFORMATION = zeroed();
        if CreateProcessWithTokenW(
            primary.0,
            LOGON_WITH_PROFILE,
            app.as_ptr(),
            args.as_mut_ptr(),
            0,
            std::ptr::null(),
            std::ptr::null(),
            &startup,
            &mut info,
        ) == 0
        {
            return Err(format!("Windows could not launch the game as a standard user: {}. Open it from its launcher.", std::io::Error::last_os_error()));
        }
        let _process = Handle(info.hProcess);
        let _thread = Handle(info.hThread);
        Ok(())
    }
}
fn standard_token() -> Result<Handle, String> {
    // UAC linked tokens can be identification-only. Bind a usable desktop token
    // to this account and reject elevated tokens, with no privileged fallback.
    unsafe {
        let shell_window = GetShellWindow();
        let mut shell_pid = 0;
        if shell_window.is_null() || GetWindowThreadProcessId(shell_window, &mut shell_pid) == 0 {
            return Err(
                "The Windows desktop is unavailable. Open the game from its launcher.".into(),
            );
        }
        let shell_process = Handle(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, shell_pid));
        let mut shell_token = std::ptr::null_mut();
        if shell_process.0.is_null()
            || OpenProcessToken(
                shell_process.0,
                TOKEN_QUERY | TOKEN_DUPLICATE,
                &mut shell_token,
            ) == 0
        {
            return Err(format!(
                "Cannot read the desktop launch token: {}",
                std::io::Error::last_os_error()
            ));
        }
        let shell_token = Handle(shell_token);
        if token_owner(shell_token.0)? != owner()? {
            return Err("The desktop belongs to a different Windows account. Open the game from its launcher.".into());
        }
        let mut elevation: TOKEN_ELEVATION = zeroed();
        let mut read = 0;
        if GetTokenInformation(
            shell_token.0,
            TokenElevation,
            (&mut elevation as *mut TOKEN_ELEVATION).cast(),
            size_of::<TOKEN_ELEVATION>() as u32,
            &mut read,
        ) == 0
            || elevation.TokenIsElevated != 0
        {
            return Err("Windows did not provide a non-elevated launch token".into());
        }
        let mut primary = std::ptr::null_mut();
        if DuplicateTokenEx(
            shell_token.0,
            TOKEN_ALL_ACCESS,
            std::ptr::null(),
            SecurityImpersonation,
            TokenPrimary,
            &mut primary,
        ) == 0
        {
            return Err(format!(
                "Cannot create a standard-user game launch token: {}",
                std::io::Error::last_os_error()
            ));
        }
        Ok(Handle(primary))
    }
}
pub fn as_standard_user<T>(f: impl FnOnce() -> Result<T, String>) -> Result<T, String> {
    if !admin() {
        return f();
    }
    let token = standard_token()?;
    unsafe {
        if ImpersonateLoggedOnUser(token.0) == 0 {
            return Err(format!(
                "Cannot restrict settings write: {}",
                std::io::Error::last_os_error()
            ));
        }
    }
    struct Revert;
    impl Drop for Revert {
        fn drop(&mut self) {
            if unsafe { RevertToSelf() } == 0 {
                std::process::abort();
            }
        }
    }
    let _revert = Revert;
    f()
}
pub fn shell(target: &str, verb: Option<&str>) -> Result<(), String> {
    unsafe {
        let t = wide(target);
        let v = verb.map(wide);
        let r = ShellExecuteW(
            std::ptr::null_mut(),
            v.as_ref().map_or(std::ptr::null(), |v| v.as_ptr()),
            t.as_ptr(),
            std::ptr::null(),
            std::ptr::null(),
            SW_SHOWNORMAL,
        );
        if (r as isize) <= 32 {
            return Err("Windows could not open the requested application".into());
        }
        Ok(())
    }
}
