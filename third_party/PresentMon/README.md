# PresentMon 2.5.1

Game Boost Pro redistributes the official standalone x64 console binary from
[GameTechDev/PresentMon](https://github.com/GameTechDev/PresentMon/releases/tag/v2.5.1)
for the user-started 15-second Frame Lab capture.

- Upstream file: `PresentMon-2.5.1-x64.exe`
- SHA-256: `9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191`
- Authenticode signer: `Intel Corporation`
- License: MIT; see `LICENSE.txt`

The normal Boost monitor does not launch PresentMon. Game Boost Pro verifies the
embedded binary hash before each capture, runs a bounded capture for the selected
process, parses the temporary CSV, then removes that CSV.
