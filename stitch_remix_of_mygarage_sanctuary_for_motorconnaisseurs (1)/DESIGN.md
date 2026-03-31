# Design System Specification: The Automotive Sanctuary

## 1. Overview & Creative North Star
**Creative North Star: "The Editorial Atelier"**

This design system rejects the noisy, rounded, and cluttered defaults of modern web applications. Instead, it draws inspiration from high-end horology, bespoke tailoring, and rare automotive galleries. We treat the digital viewport not as a "website," but as a physical exhibition space. 

The aesthetic is defined by **Architectural Brutalism meets Classical Elegance**. We achieve this through "Hard-Edge Luxury"—a strict adherence to 0px border radii—and a reliance on intentional asymmetry. By utilizing heavy whitespace (negative space) and alternating section layouts, we create a rhythmic "scroll" that mimics the experience of walking through a private car collection. The goal is to feel curated, silent, and authoritative.

---

## 2. Colors & Surface Philosophy
The palette is a sophisticated interplay of organic creams, deep forest greens, and metallic accents. It is designed to feel "analog" rather than digital.

### Core Palette
- **Primary (Gold):** `#775a19` (Brand core) / `#c5a059` (Interaction highlight)
- **On-Background (Forest Green):** `#1d1c16` (Deep, authoritative text)
- **Surface (Cream/Beige):** `#fef9ee` (The base "paper" of the gallery)

### The "No-Line" Rule
Traditional 1px solid borders are strictly prohibited for sectioning. Structural boundaries must be defined solely through:
1.  **Background Color Shifts:** Transitioning from `surface` to `surface-container-low` (#f8f3e9) to signal a new content block.
2.  **Tonal Transitions:** Using the `surface-variant` (#e7e2d8) to create a "recessed" area for technical specifications or data tables.

### Surface Hierarchy & Nesting
Treat the UI as a series of stacked, high-grade materials. To create depth without shadows:
- **Level 1 (Base):** `surface` (#fef9ee)
- **Level 2 (Inlay):** `surface-container` (#f2ede3)
- **Level 3 (Highlight):** `surface-container-highest` (#e7e2d8)
*Example: A technical spec card should not have a shadow; it should be an inlay of `surface-container-high` sitting inside a `surface` section.*

---

## 3. Typography
Our typography is the primary driver of the brand's luxury positioning. We pair a high-contrast serif for narrative with a technical sans-serif for utility.

- **Display & Headlines (Newsreader):** A sophisticated serif. Use `display-lg` (3.5rem) with wide tracking for a cinematic feel. This conveys heritage and exclusivity.
- **Body & Technical (Manrope/Inter):** A clean, modern sans-serif. Use `body-md` (0.875rem) for descriptions. 
- **The Editorial Rule:** Headings in `on-surface` (Forest Green) should frequently use asymmetric placement (e.g., left-aligned in a 12-column grid starting at column 2) to break the "template" feel.

---

## 4. Elevation & Depth
In a world of "soft" design, we embrace "hard" layering. 

- **The Layering Principle:** Depth is achieved by stacking color tokens. Place a `surface-container-lowest` (#ffffff) element on a `surface-container-low` (#f8f3e9) background to create a crisp, natural lift.
- **Ambient Shadows:** Only used for floating "Utility Modals" (e.g., a car configurator panel). Shadows must be extremely diffused: `box-shadow: 0 20px 50px rgba(29, 28, 22, 0.05)`.
- **The "Ghost Border" Fallback:** If a border is required for input clarity, use the `outline-variant` (#d1c5b4) at 20% opacity. 
- **Glassmorphism:** For top navigation bars, use `surface` at 80% opacity with a `backdrop-blur(20px)`. This allows the vibrant colors of vehicle photography to bleed through the UI subtly.

---

## 5. Components

### Buttons
- **The Signature Ghost Button:** 0px radius, 1px border using `primary_container` (#c5a059). Text must be `label-md`, UPPERCASE, with 0.1rem letter spacing.
- **Primary Button:** Solid `primary` (#775a19) fill, white text, 0px radius. Use sparingly for "Reserve Now" or "Inquire."
- **Hover States:** On hover, the ghost button should transition to a subtle `primary_fixed` (#ffdea5) background at 10% opacity.

### Cards & Lists
- **Forbid Dividers:** Do not use horizontal lines to separate list items. Use the spacing scale (`8` / 2.75rem) to create separation through "breathing room."
- **Image Treatment:** All images must be square-edged. Consider a subtle `1px` inner glow on images to give them a "printed" look.

### Input Fields
- **Styling:** Underline-only or subtle `surface-variant` backgrounds. 
- **Focus State:** The bottom border transitions from `outline-variant` to `primary` (Gold). No "glow" or rounded focus rings.

### Signature Component: The "Spec Sheet" Grid
A 4-column grid for automotive data (HP, 0-60, Torque) using `display-sm` for the values and `label-sm` (uppercase) for the units, separated by large vertical whitespace (`12` / 4rem) instead of borders.

---

## 6. Do’s and Don’ts

### Do:
- **Embrace Asymmetry:** Offset your text blocks. Let a heading sit on the left while the body text sits on the right of the container.
- **Use Square Edges:** Every element, from buttons to tooltips, must have a 0px radius.
- **Respect the Grid:** Use heavy vertical spacing (`24` / 8.5rem) between major sections to let the cars "breathe."

### Don’t:
- **Don’t use 100% Black:** Always use the Dark Forest Green (`#1d1c16`) for text to maintain a high-end, organic feel.
- **Don’t use Icons in Circles:** If an icon needs a background, use a square or a simple Ghost Border.
- **Don’t use Standard Shadows:** Avoid "Material Design" style drop shadows. Rely on color-blocking for hierarchy.
- **Don't use Centered Content exclusively:** Luxury is about intentionality; use left-aligned editorial layouts to avoid a "generic" web look.