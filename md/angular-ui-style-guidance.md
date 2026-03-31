# Keep The Existing Angular UI Style

Use this prompt when you want new Angular components or pages to match the current ApplyVault dashboard design.

## Recommended Request

Create `[component/page name]` in the existing ApplyVault dashboard style.

Keep the current dark premium look:
- same typography
- same spacing and rounded corners
- same glassy cards and soft gradients
- same button and input styling
- same overall visual tone

Please extend the current design system instead of creating a different style.
Use clean Angular practices, separation of concerns, and reusable components.

## Shorter Version

Build the next component in the same visual style as the current dashboard.
Keep the same color palette, typography, spacing rhythm, border radius, glassy dark surfaces, gradient accents, and card treatment.
Reuse the existing design language instead of inventing a new one.

## Helpful Extra Phrases

- Match the existing UI style exactly.
- Reuse shared styles and tokens where possible.
- Do not redesign the page, just extend the current design system.
- Keep the same Angular structure and separation of concerns.

## Best Long-Term Approach

For stronger consistency across future components, extract the current styling into a small design system:

- SCSS variables for colors, spacing, radius, and shadows
- shared utility classes or mixins for cards, pills, buttons, and inputs
- a `shared/ui` folder for reusable presentational components

This makes future components look consistent by default, not just by prompt wording.
