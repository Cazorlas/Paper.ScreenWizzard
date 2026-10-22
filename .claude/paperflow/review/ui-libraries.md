# UI library catalog

Starting points checked 2026-10-22; verify the chosen version before proposing a library.

- In a WPF project the project's own WPF style skill wins; WPF libraries are only for a new project or when the owner asks.
- A proposal names the library, why it fits this screen, and its licence. It never adds a second component set beside
  the one the project already uses; prefer the project's set, then a primitive library of the same framework.
- Commons Clause sets: copy one component with its notice, port to the project's framework rather than adding React.
- Every motion proposal honours prefers-reduced-motion.
- The table is a starting point checked on 2026-10-22; a reader may cite any other library with its licence.

- Proposals that cite a library state its licence. Commons Clause sets are copied one component at a time with notice; never sell or redistribute the set itself.
- Animate.css uses Hippocratic License 2.1, which is not OSI open source; do not propose it for product code.
- Canvas UI requires checking the repository licence before proposing it.

## React

| Library | Use for | Licence | Link |
| --- | --- | --- | --- |
| shadcn/ui | copy-in accessible components on Radix + Tailwind | MIT | https://github.com/shadcn-ui/ui |
| Radix Primitives | unstyled accessible primitives (dialog, menu, popover) | MIT | https://github.com/radix-ui/primitives |
| Magic UI | animated marketing components | MIT | https://github.com/magicuidesign/magicui |
| React Bits | animated text/backgrounds/effects | MIT + Commons Clause | https://github.com/DavidHDev/react-bits |
| Motion (framer-motion) | animation and gestures | MIT | https://github.com/motiondivision/motion |
| Mantine | full component set, hooks, forms | MIT | https://github.com/mantinedev/mantine |
| HeroUI | themed component set on Tailwind | Apache-2.0 | https://github.com/heroui-inc/heroui |
| Chakra UI | themed accessible component set | MIT | https://github.com/chakra-ui/chakra-ui |

## Vue

| Library | Use for | Licence | Link |
| --- | --- | --- | --- |
| shadcn-vue | Vue port of shadcn/ui, copy-in components | MIT | https://github.com/unovue/shadcn-vue |
| Reka UI (ex Radix Vue) | unstyled accessible primitives | MIT | https://github.com/unovue/reka-ui |
| Nuxt UI | Tailwind component set on Reka UI (works in plain Vue too) | MIT | https://github.com/nuxt/ui |
| Inspira UI | animated components (Vue port of Aceternity / Magic UI style) | MIT | https://github.com/unovue/inspira-ui |
| Vue Bits | animated text/backgrounds/effects | MIT + Commons Clause | https://github.com/DavidHDev/vue-bits |
| Motion for Vue (motion-v) | animation and gestures | MIT | https://github.com/motiondivision/motion-vue |
| Element Plus | dense admin/data components | MIT | https://github.com/element-plus/element-plus |
| Naive UI | full themeable set, TypeScript | MIT | https://github.com/tusen-ai/naive-ui |
| PrimeVue | large set incl. data table, unstyled mode | MIT | https://github.com/primefaces/primevue |
| Vuetify | Material Design set | MIT | https://github.com/vuetifyjs/vuetify |
| Quasar | set + app shells (SPA, PWA, Electron) | MIT | https://github.com/quasarframework/quasar |

## Svelte

| Library | Use for | Licence | Link |
| --- | --- | --- | --- |
| shadcn-svelte | Svelte port of shadcn/ui | MIT | https://github.com/huntabyte/shadcn-svelte |
| Bits UI | unstyled accessible primitives | MIT | https://github.com/huntabyte/bits-ui |
| Skeleton | Tailwind design system | MIT | https://github.com/skeletonlabs/skeleton |
| Svelte Bits | animated effects | MIT + Commons Clause | https://github.com/DavidHDev/svelte-bits |

## Angular

| Library | Use for | Licence | Link |
| --- | --- | --- | --- |
| Angular Material / CDK | Material components, CDK a11y primitives | MIT | https://github.com/angular/components |
| Spartan | shadcn-style components for Angular | MIT | https://github.com/spartan-ng/spartan |
| PrimeNG | large set | community MIT, LTS commercial - check version | https://github.com/primefaces/primeng |

## Plain JS/CSS

| Library | Use for | Licence | Link |
| --- | --- | --- | --- |
| Pico CSS | classless semantic styling | MIT | https://github.com/picocss/pico |
| daisyUI | Tailwind component classes | MIT | https://github.com/saadeghi/daisyui |
| Canvas UI (DavidHDev) | plain-JS canvas effects | check repo licence | https://github.com/DavidHDev/canvas-ui |

## WPF/Avalonia

| Library | Use for | Licence | Link |
| --- | --- | --- | --- |
| WPF UI (lepoco) | Fluent/Windows 11 look | MIT | https://github.com/lepoco/wpfui |
| MaterialDesignInXamlToolkit | Material look | MIT | https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit |
| Avalonia | cross-platform XAML UI | MIT | https://github.com/AvaloniaUI/Avalonia |
| Ursa.Avalonia | extra controls | MIT | https://github.com/irihitech/Ursa.Avalonia |

## Flutter

| Library | Use for | Licence | Link |
| --- | --- | --- | --- |
| Flutter Material/Cupertino | built-in widgets | BSD-3-Clause | https://github.com/flutter/flutter |
| fl_chart | charts | MIT | https://github.com/imaNNeo/fl_chart |

## SwiftUI/Compose

| Library | Use for | Licence | Link |
| --- | --- | --- | --- |
| Material Components for Android | Material views/Compose theming | Apache-2.0 | https://github.com/material-components/material-components-android |
| SwiftUIX | extra SwiftUI components | MIT | https://github.com/SwiftUIX/SwiftUIX |

