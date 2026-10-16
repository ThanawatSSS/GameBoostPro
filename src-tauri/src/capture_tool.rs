use crate::windows::{self, Handle};
use sha2::{Digest, Sha256};
use std::{
    fs::{File, OpenOptions},
    io::{Read, Write},
    os::windows::fs::OpenOptionsExt,
    path::{Path, PathBuf},
};
use windows_sys::Win32::{
    Foundation::*,
    Security::{Authorization::*, *},
    Storage::FileSystem::*,
    System::Com::CoTaskMemFree,
    UI::Shell::*,
};

const PRESENTMON_SHA256: &str = "9bec3083069f58f911e6a512f4806db51a27bd096103087bc1d05ef54c80a191";
pub struct CaptureTool {
    path: PathBuf,
    directory: PathBuf,
    image: Option<File>,
    directory_lock: Option<Handle>,
}
impl CaptureTool {
    pub fn prepare(source: &Path) -> Result<Self, String> {
        let mut bytes = Vec::new();
        File::open(source)
            .map_err(|_| {
                "PresentMon is missing. Install the full app or extract the complete ZIP."
            })?
            .take(2_000_001)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        if bytes.len() > 2_000_000 || format!("{:x}", Sha256::digest(&bytes)) != PRESENTMON_SHA256 {
            return Err("PresentMon checksum did not match the trusted Intel release".into());
        }
        let root = program_data()?;
        let directory = root.join(format!("GameBoostPro-Capture-{}", uuid::Uuid::new_v4()));
        protected_directory(&directory)?;
        let mut tool = Self {
            path: directory.join("PresentMon.exe"),
            directory,
            image: None,
            directory_lock: None,
        };
        unsafe {
            let handle = Handle(CreateFileW(
                windows::wide(&tool.directory.to_string_lossy()).as_ptr(),
                FILE_READ_ATTRIBUTES,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                std::ptr::null(),
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
                std::ptr::null_mut(),
            ));
            if handle.0 == INVALID_HANDLE_VALUE {
                return Err("Cannot lock the protected capture directory".into());
            }
            tool.directory_lock = Some(handle);
        }
        let mut image = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&tool.path)
            .map_err(|e| e.to_string())?;
        image
            .write_all(&bytes)
            .and_then(|_| image.sync_all())
            .map_err(|e| e.to_string())?;
        drop(image);
        let mut locked = OpenOptions::new()
            .read(true)
            .share_mode(FILE_SHARE_READ)
            .open(&tool.path)
            .map_err(|e| e.to_string())?;
        let mut verifier = Sha256::new();
        std::io::copy(&mut locked, &mut verifier).map_err(|e| e.to_string())?;
        if format!("{:x}", verifier.finalize()) != PRESENTMON_SHA256 {
            return Err("Staged PresentMon integrity check failed".into());
        }
        tool.image = Some(locked);
        Ok(tool)
    }
    pub fn path(&self) -> &Path {
        &self.path
    }
}
impl Drop for CaptureTool {
    fn drop(&mut self) {
        self.image.take();
        let _ = std::fs::remove_file(&self.path);
        self.directory_lock.take();
        let _ = std::fs::remove_dir(&self.directory);
    }
}
fn program_data() -> Result<PathBuf, String> {
    unsafe {
        let mut value = std::ptr::null_mut();
        if SHGetKnownFolderPath(&FOLDERID_ProgramData, 0, std::ptr::null_mut(), &mut value) < 0
            || value.is_null()
        {
            return Err("Windows ProgramData is unavailable".into());
        }
        let mut len = 0;
        while len < 32768 && *value.add(len) != 0 {
            len += 1;
        }
        let path = PathBuf::from(String::from_utf16_lossy(std::slice::from_raw_parts(
            value, len,
        )));
        CoTaskMemFree(value.cast());
        if !path.is_absolute() {
            return Err("Windows returned an invalid ProgramData path".into());
        }
        Ok(path)
    }
}
fn protected_directory(path: &Path) -> Result<(), String> {
    unsafe {
        // Administrators own the directory; the medium same-user token is not its owner.
        let sddl = windows::wide("O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)");
        let mut descriptor = std::ptr::null_mut();
        if ConvertStringSecurityDescriptorToSecurityDescriptorW(
            sddl.as_ptr(),
            1,
            &mut descriptor,
            std::ptr::null_mut(),
        ) == 0
        {
            return Err("Cannot create capture directory security descriptor".into());
        }
        let attributes = SECURITY_ATTRIBUTES {
            nLength: std::mem::size_of::<SECURITY_ATTRIBUTES>() as u32,
            lpSecurityDescriptor: descriptor,
            bInheritHandle: 0,
        };
        let created =
            CreateDirectoryW(windows::wide(&path.to_string_lossy()).as_ptr(), &attributes);
        LocalFree(descriptor.cast());
        if created == 0 {
            return Err("Cannot create an exclusive Administrator capture directory".into());
        }
        Ok(())
    }
}
