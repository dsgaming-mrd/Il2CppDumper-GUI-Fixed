# Il2CppDumper GUI Fixed

![Il2CppDumper GUI](Screenshot.png)  

This is a **Unity Il2CppDumper GUI** that I developed based on the original source code by **Perfare**.  
This version has been optimized and some minor bugs have been fixed to work better with games that have **Metadata** errors.  

✅ Runs on **Windows**  
✅ Supports both **Android** and **iOS**  
✅ Intuitive and easy-to-use GUI interface  
✅ Can generate **structs**, **dummy dlls**, and export scripts for IDA/Ghidra/Hopper   

---

## 🚀 How to Use  

1. Prepare files:  
   - `libil2cpp.so` (Android) or the main game `Executable file` (iOS)  
   - `global-metadata.dat`  

2. Run `Il2CppDumper GUI`  

3. Select:  
   - **Executable file** → point to `libil2cpp.so` or the main game `Executable file`  
   - **global-metadata.dat** → point to the game's metadata  
   - **Output directory** → choose the folder to save the results  

4. Press **Start** → the tool will automatically:  
   - Dump data  
   - Generate structs  
   - Generate dummy dll  
   - Copy analysis support scripts to the output folder  

5. Open the `dump.cs` file or dummy dll with **dnSpy/ILSpy** for analysis.  

---

## 🙏 Credits  

- **[Perfare](https://github.com/Perfare)** – Original author of Il2CppDumper  
- **[AndnixSH](https://github.com/AndnixSH)** – Author of the previous GUI version  
- **Mr D - DS Gaming (VNC Team)** – Developer of the custom GUI version (Android/iOS)