![Windows](https://img.shields.io/badge/Windows-7%2B-0078D6?logo=windows&logoColor=white) ![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white) ![C%23](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white) ![Platform](https://img.shields.io/badge/Platform-x64-lightgrey) ![License](https://img.shields.io/github/license/UncleRiot/c2flux) ![Release](https://img.shields.io/github/v/release/UncleRiot/c2flux)

# c² flux – Tree Scanner

**A fast and lightweight disk space analyzer for Windows.**

c² flux – Tree Scanner introduces a completely redesigned user interface and significantly improved performance.
<br><br>



---

❤️ If you’d like to support my work with a small contribution, I’d really appreciate it. ❤️
<br><br>
<a href="https://ko-fi.com/uncleriot"><img src="https://github.com/user-attachments/assets/57680fed-c0b7-44fa-ac74-076903bd7eec" alt="Support me on Ko-fi" width="174"></a>

---

<br>
> **Security and packaging note:** VirusTotal and other automated analysis platforms may report heuristic findings for c² flux. A short explanation of these findings and the current evaluation of single-file versus multi-file releases is available here: [Security scanner findings and release packaging](Security-scanner-findings-and-release-packaging).
<br><br>


## Windows SmartScreen warning

<img width="438" height="173" alt="grafik" src="https://github.com/user-attachments/assets/429951bb-ab24-49a3-89a8-ecb4b0a7fec1" />

Windows SmartScreen may block c² flux because the application is not yet widely recognized.

This warning does not automatically mean that the application is malicious.

Only continue if you downloaded c² flux from the official GitHub repository.

### How to start c² flux

1. Click **More info**.
2. Click **Run anyway**.
3. Confirm the Windows security prompt, if one appears.
4. c² flux will start.

> **Important:** Do not run the application if you downloaded it from an unknown or untrusted source.

<br><br>
## Highlights

- Strong caching mechanisms for faster repeated scans
- Regular and MFT-based scanning
  - **MFT scan:** Fastest possible scan mode when running as Administrator
  - **Regular scan:** Fast first scan, super-fast subsequent scans through caching
- Multiple visualization and display styles
- Export functions for scan and analysis data
- Scan history and storage timeline
- Optional SQLite database for persistent scan storage
  - Store previous scans
  - Compare scans
  - Inspect detailed differences between scan states
- Super-fast global search
  - Find files and folders instantly
  - Integrated context menu actions
- Windows Explorer integration
- Light and dark themes
- Multilingual interface with 30 languages

> **c² flux – Tree Scanner**

<img width="1508" height="855" alt="c² flux – Tree Scanner" src="https://github.com/user-attachments/assets/47b00346-3bf8-4a43-ad54-8a0e970da33a" />

<br>
<br>

📖 **Documentation:** See the [GitHub Wiki](https://github.com/UncleRiot/c2flux/wiki) for instructions and usage guides.
<br>

## Requirements

- Windows 7 or newer
- Windows x64
- .NET 8 Desktop Runtime

> NTFS MFT scanning requires administrator privileges.

## License

Licensed under the [GNU General Public License v3.0](LICENSE).
