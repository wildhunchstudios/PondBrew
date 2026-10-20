---
name: unity-game-ui-ux
description: Implement or review Evolution Shop HUD and menus using its verified native Unity UI framework. Use for layout, readability, interaction states and Figma-to-game design translation.
---

# Unity Game Ui Ux

Read AGENTS.md current task and inspect Runtime/ShopHud.cs plus affected menu partials. The verified HUD uses IMGUI OnGUI with the Unity Input System; uGUI being installed does not mean the HUD uses it. Re-detect the actual changed surface before choosing APIs. No React or browser framework. The user approved a UI Toolkit rebuild on 2026-10-19 (AGENTS.md UX.1–UX.12); until UX-09 retires IMGUI, check which path a screen uses before editing it.

Inputs: the screen/state and player decision, current screenshots, supported resolution/input, and permitted Figma design context if provided. Figma installation is not account authorization; no production file writes or screenshot uploads occur merely to test setup. Interpret Figma layout/tokens as design input, not automatic Unity import.

Protect the view of creatures/stations. Prioritize day/phase, cash, next action, customer/queue needs and focused E interaction. Check long values, all four culture counters, empty/full/blocked states, pricing feedback, confirmation/cancellation and menu scrolling. Preserve existing input capture, pause and safe transaction commands. Visual controls must not independently mutate money or stock.

Keep text contrast in every GUIStyle state (normal/hover/active/focused and toggled states); inspect actual rendered button labels. Exercise real mouse/keyboard clicks and focus where relevant, plus Escape/back/close and settings rebinding. Keyboard/mouse is the current platform contract. Check controller navigation only if the affected feature supports or explicitly adds it; report unsupported coverage honestly.

Capture real 1280x720 and 1920x1080 layouts, relevant aspect/scaling extremes and overlay combinations. Distinguish synthetic input, direct application commands and human use. Allow future localization expansion without claiming translated coverage. Record screen/build, states tested, input method, overlap/occlusion and readability limitations.

Output: the native implementation/design critique, screenshots and actual interaction evidence, then AGENTS.md status. Stop for missing design access only on dependent Figma work; continue independent local UI work. Do not call a mockup proof of native rendering or change the approved art direction.
