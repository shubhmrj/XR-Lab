# 🧪 XR Chemistry Lab

<div align="center">

![XR-Chemistry-Lab-Banner](https://via.placeholder.com/800x200/4D7DFF/FFFFFF?text=XR+Chemistry+Lab+-+Perform+Experiments+with+Gestures)
*🚀 (Replace this banner with a screenshot of your lab environment! See instructions below.)*

[![Unity](https://img.shields.io/badge/Unity-2022+-black?logo=unity)](https://unity.com)
[![XR](https://img.shields.io/badge/XR-AR%20%7C%20VR-blue)](#)
[![Contributions](https://img.shields.io/badge/Contributions-Welcome-orange)](docs/Contributing.md)
[![GitHub issues](https://img.shields.io/github/issues/shubhmrj/XR-Lab)](https://github.com/shubhmrj/XR-Lab/issues)
[![GitHub forks](https://img.shields.io/github/forks/shubhmrj/XR-Lab)](https://github.com/shubhmrj/XR-Lab/network)
[![GitHub stars](https://img.shields.io/github/stars/shubhmrj/XR-Lab)](https://github.com/shubhmrj/XR-Lab/stargazers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)


</div>

Welcome to the **XR Chemistry Lab**, an immersive AI powered Extended Reality experience built in Unity. Step into a virtual laboratory where you can conduct complex chemistry experiments intuitively using natural hand gestures—no controllers required! This project demonstrates the future of interactive education and simulation.

---

## 🌍 Overview
XR Reality Chemistry Lab is an **Augmented Reality + Virtual Reality educational app** where students can interact with **3D chemical experiments** using **hand gestures powered by ManoMotion SDK**.

An **Extended Reality (XR) Project** that combines **Augmented Reality (AR), Virtual Reality (VR), and Mixed Reality (MR)** technologies to deliver immersive learning, simulations, and interaction experiences.  

This repository is built for **research, experiments, and application development** in XR using modern tools such as **Unity, ManoMotion SDK, AR Foundation, and XR Interaction Toolkit**.

---

## 📌 Features

- 🌐 **XR Integration** – Support for AR, VR, and MR.  
- ✋ **Hand Tracking & Gestures** (via ManoMotion SDK).  
- 🧪 **Interactive Simulations** – e.g., AR Chemistry Lab experiments.  
- 🎮 **Unity XR Toolkit** for cross-platform XR development.  
- 📱 **Multi-device Support** – Android, iOS, and VR headsets (Meta Quest, HTC Vive, etc.).  
- 📊 **Immersive UI/UX** – Gesture-based interactions, 3D objects, overlays.  
- ⚡ **Optimized Performance** for real-time rendering and interaction.  

### 🖐️ Intuitive Gesture Control
*   **Pinch to Select**: Precisely pick up beakers, test tubes, and other equipment.
*   **Grab to Manipulate**: Hold and move objects around the lab space.
*   **Pour Gesture**: Tilt your hand to pour liquids from one container to another.
*   **Stir Gesture**: Mimic a stirring motion to mix solutions.
*   **Ignite Gesture**: Snap your fingers to light a Bunsen burner.

### 🧭 Immersive Virtual Lab
*   **Realistic Equipment**: Interact with beakers, flasks, burners, and more.
*   **Dynamic Chemical Reactions**: Witness color changes, precipitates, bubbles, and heat effects based on real chemistry.
*   **Multi-Step Experiments**: Follow guided procedures or experiment freely.

### 🛠️ Technical Highlights
*   Built with **Unity** and **C#** for cross-platform compatibility.
*   Utilizes **Meta's Hand Tracking API** for high-fidelity gesture recognition on Quest devices.
*   Clean, modular codebase for easy extension and customization.

---

## 🛠️ Tech Stack

| Component | Technology |
| :--- | :--- |
| **Game Engine** | Unity 2022.3+ |
| **Programming Language** | C# |
| **XR Platform** | Meta Quest 2/3/Pro (Native OpenXR) |
| **Gesture Recognition** | OVRHand, OVRSkeleton, Custom C# Logic |
| **UI Framework** | Unity UI (Canvas) |

---

## 📦 Installation & Setup

### Prerequisites
1.  **Unity Hub** and **Unity Editor** (version 2022.3.16f1 or later).
2.  A **Meta Quest 2/3/Pro** headset.
3.  (Optional) **Android Build Support** module installed in Unity.

### Steps to Run
1.  **Clone the Repository**
    ```bash
    git clone https://github.com/shubhmrj/XR-Lab.git
    cd XR-Lab
    ```

2.  **Open the Project in Unity**
    *   Open Unity Hub.
    *   Click `Add project from disk` and select the cloned folder.
    *   Ensure the correct Unity version is used.

3.  **Configure Project Settings**
    *   Go to `File > Build Settings`.
    *   Switch the platform to **Android**.
    *   Ensure **Texture Compression** is set to `ASTC`.
    *   In `Player Settings > XR Plug-in Management`, install and enable **OpenXR**. Under the Android tab, add the `Oculus Touch Controller Profile` feature.

4.  **Enable Hand Tracking**
    *   In `Project Settings`, navigate to `XR Plug-in Management > OpenXR` (Android tab).
    *   Click the `+` button under **Interaction Profiles** and add `Oculus Touch Controller Profile` and `Meta Hand Tracking Aim` profile.
    *   In the `Quest` settings, set `Hand Tracking Support` to `Controllers and Hands` or `Hands Only`.

5.  **Build and Deploy**
    *   Connect your Quest headset to your PC via a link cable (enable Link when prompted on the headset).
    *   In Unity, click `Build And Run`. The APK will be built and installed directly on your headset.

---

## 🎮 How to Use / Gesture Guide

Once the application is running on your headset:

1.  **Calibrate Your Hands**: Hold your hands in view of the headset's cameras until they are fully tracked.
2.  **Navigate the Lab**: Use your gaze or pinch to point and select UI elements.
3.  **Perform Experiments**:
    *   **Select Object**: Pinch your index finger and thumb together over an object.
    *   **Move Object**: While pinching, move your hand to relocate the object.
    *   **Pour Liquid**: Pinch a container, tilt your hand past a certain angle to pour.
    *   **Mix**: Hold a beaker and make a slow, circular stirring motion.
    *   **Ignite Burner**: Snap your fingers near the Bunsen burner.

---

## 📸 Screenshots & Media

*(Replace the links below with your own screenshots and videos!)*

| Experiment Preview | Gesture in Action |
| :---: | :---: |
| [![Screenshot 1](https://via.placeholder.com/300x200/FF6B6B/FFFFFF?text=Acid+Base+Reaction)](./Media/screenshot1.jpg) | [![Screenshot 2](https://via.placeholder.com/300x200/4D7DFF/FFFFFF?text=Pour+Gesture)](./Media/screenshot2.jpg) |
| *Mixing an acid and base* | *Pouring a solution* |

| Lab Overview | UI Interaction |
| :---: | :---: |
| [![Screenshot 3](https://via.placeholder.com/300x200/34D399/FFFFFF?text=Full+Lab+View)](./Media/screenshot3.jpg) | [![Screenshot 4](https://via.placeholder.com/300x200/F472B6/FFFFFF?text=Experiment+Menu)](./Media/screenshot4.jpg) |
| *The main lab environment* | *Selecting an experiment* |

**🔽 Download Links:**
*   [Watch a Demo Video](https://drive.google.com/your-demo-video-link-here) (Upload to Google Drive/YouTube)
*   [Download APK (v1.0.0)](https://drive.google.com/your-apk-download-link-here) (For sideloading on Quest)

---

## 🗂️ Project Structure


```
XR-Lab/
│── Assets/              # Unity assets (models, scripts, prefabs)
│── Docs/                # Documentation, diagrams, reports
│── Scripts/             # Core C# scripts for XR functionality
│── Scenes/              # Unity scenes for experiments/simulations
│── Plugins/             # ManoMotion SDK, XR Interaction Toolkit
│── README.md            # Project documentation
```

---

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Feel free to check the [issues page](https://github.com/shubhmrj/XR-Lab/issues).

1.  Fork the Project.
2.  Create your Feature Branch (`git checkout -b feature/AmazingFeature`).
3.  Commit your Changes (`git commit -m 'Add some AmazingFeature'`).
4.  Push to the Branch (`git push origin feature/AmazingFeature`).
5.  Open a Pull Request.

---

## 📜 License

This project is distributed under the **MIT License**. See the `LICENSE` file for more information.

---

## 🙏 Acknowledgments

*   **Meta** for the incredible Quest platform and robust Hand Tracking API.
*   **Unity Technologies** for the powerful game engine and XR tools.
*   Chemistry educators and students for inspiration.

---

## 📞 Contact

Shubham Raj - [@shubhmrj](https://github.com/shubhmrj) - shubhmrj@example.com

Project Link: [https://github.com/shubhmrj/XR-Lab](https://github.com/shubhmrj/XR-Lab)

## 👨‍💻 Author

**Shubham** – Data Scientist  
🔗 [GitHub Profile](https://github.com/shubhmrj)