# Troubleshooting

## Why does my scan take so long?

In most cases, c2flux should be able to scan a drive relatively quickly.

However, scan time can vary significantly depending on:

- the type of drive
- the number of files and folders
- the selected scan options
- the permissions available to c2flux
- which scan method is used internally
- whether c2flux has to fall back to a slower scan method

A mechanical hard drive (HDD) can be significantly slower than an SSD, especially when the scan has to access a large number of folders individually.

---

## 1. Check your scan settings

Before investigating further, verify that you are only scanning what you actually need.

Open the c2flux options and check the configured scan settings.

Some options may increase the amount of work required during a scan.

> **Screenshot:** Scan options  
<img width="391" height="369" alt="grafik" src="https://github.com/user-attachments/assets/c2781d92-16a1-4130-b2e7-3b29c68b7c77" />


If possible, try another scan with only the required options enabled and compare the scan time.

---

## 2. Check whether c2flux is using the fast scanner

For NTFS drives, c2flux can use a fast MFT-based scan method.

If this method cannot be used or fails, c2flux may automatically fall back to another scan method.

The fallback scan can be much slower, especially on:

- mechanical hard drives
- drives containing many files
- deeply nested folder structures
- large system drives

For example, a scan that normally finishes quickly may take several minutes when a slower fallback method is used.

This does not necessarily mean that c2flux has frozen.

---

## 3. Run c2flux with the required permissions

Some scan methods may require additional permissions.

If you experience unusually slow scans, try running c2flux as Administrator and repeat the scan.

Then compare the scan time.

> **Screenshot:** Run as Administrator  
<img width="271" height="367" alt="grafik" src="https://github.com/user-attachments/assets/0a3fdb81-6d7a-4aaf-acb2-b23da63e86bb" />


---

## 4. Enable logging

If the scan is still unusually slow, enable logging before starting another scan.

The log can help determine:

- which scanner was used
- whether the preferred scanner failed
- whether c2flux switched to a fallback scanner
- how long the scan took
- whether errors occurred during the scan

> **Screenshot:** Enable logging  
<img width="391" height="369" alt="grafik" src="https://github.com/user-attachments/assets/9b8aaab9-d882-4fe9-8dfc-b6c86c0f78c6" />


After enabling logging:

1. Restart c2flux if required.
2. Start the same scan again.
3. Wait until the scan has finished.
4. Keep the generated log file.

---

## 5. Information to include when reporting the problem

If you report a slow scan, please include as much of the following information as possible:

- c2flux version
- Windows version
- drive type: HDD, SSD, NVMe, USB drive, etc.
- file system, for example NTFS
- approximate drive size
- approximate amount of used space
- what you scanned:
  - complete drive
  - single folder
  - network location
- approximate scan duration
- whether c2flux was started as Administrator
- the scan options you used
- the generated log file

Example:

> **Drive:** 2 TB HDD  
> **File system:** NTFS  
> **Used space:** approximately 1.4 TB  
> **Scan:** complete drive  
> **Scan time:** approximately 10 minutes  
> **Administrator:** Yes  
> **c2flux version:** x.x.x

This information makes it much easier to determine whether the scan time is expected or whether c2flux is using a slower fallback method.

---

## 6. HDD vs. SSD performance

Mechanical hard drives are especially sensitive to workloads that require accessing many different folders and files.

If c2flux cannot use the fast NTFS/MFT scan path, the difference between an HDD and an SSD can become very noticeable.

Therefore:

- a slower scan on an HDD can be expected
- a very large difference compared with another system should still be investigated
- the log is the best way to determine which scan method was actually used

---

## Still having problems?

If the scan still appears unusually slow after checking the steps above, open a GitHub issue and attach the relevant information and log.

Please describe what was scanned and how long it took.

The more information you provide, the easier it is to reproduce and diagnose the problem.
