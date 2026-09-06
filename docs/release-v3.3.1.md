# Game Boost Pro 3.3.1

## แก้การอัปเดตและทางลัดซ้ำ
- อัปเดตทับตำแหน่งติดตั้งเดิม และคงโปรไฟล์ของผู้ใช้ไว้
- รวมทางลัด Start Menu ของ Game Boost Pro ที่ตรวจยืนยันได้ ให้เปิดตัวที่ติดตั้งล่าสุด
- ไม่ลบไฟล์ Portable เก่าหรือทางลัดของโปรแกรมอื่น
- สำรองไฟล์ก่อนแทนที่ และคืนไฟล์เดิมเมื่อการแทนที่ไฟล์ล้มเหลว
- แสดงเลขเวอร์ชันตรงกันในแอป ตัวติดตั้ง และไฟล์ Portable

## UX และโปรไฟล์
- หน้าหลัก Native ปรับตามขนาดหน้าต่าง พร้อม TH/EN และฟอนต์ไทย
- Master / Override แยกชัดเจน พร้อม Light, Balanced และ Performance
- Graphics แยกค่าที่บันทึกได้จากข้อมูลความเข้ากันได้ของ GPU
- ตรวจหา NVIDIA App และ Control Panel แบบ Desktop / Store แยกกัน
- Frame Lab เปิดจากหน้าหลักได้ และกลับไป Boost / Restore ระหว่างการวัด A/B ได้
- Admin เป็นสถานะ ไม่ใช่ปุ่มที่กดแล้วไม่ทำงาน
- อ่าน CPU ด้วย native system timing และพัก telemetry เมื่อซ่อนหน้าต่าง

## Supported
- Acer laptops with NitroSense
- Desktop PCs
- Windows 10/11 x64
- Other laptops remain blocked pending platform-specific validation.

## Downloads
- **GameBoostPro-Setup.exe**: install or update the existing installation.
- **GameBoostPro-Portable-v3.3.1.zip**: extract the entire ZIP, including the tools folder.
- **SHA256SUMS.txt**: SHA-256 checksums for the downloadable files.

Restore any active Boost session and exit the running app before updating.
Profiles are preserved. Setup requests Administrator permission. The Game Boost
executables are not Authenticode-signed; the bundled PresentMon component retains
its Intel signature and pinned hash.

Release tests cover installer rollback/idempotence, shortcut ownership, profile
and recovery policies, GUI interactions, TH/EN layouts, packaging and performance
budgets. No measured FPS gain, 24-hour gaming result or universal stutter reduction
is claimed. DLSS / Reflex remain in-game settings, and there are no new service,
security, fan, network or overclocking tweaks.
