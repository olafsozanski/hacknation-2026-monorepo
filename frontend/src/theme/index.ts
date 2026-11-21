import { createSystem, defaultConfig, defineConfig } from '@chakra-ui/react'
import { checkboxAnatomy, datePickerAnatomy, nativeSelectAnatomy, segmentGroupAnatomy } from '@chakra-ui/react/anatomy'

const hc = (base: string, highContrast: string) => ({ value: { base, _light: base, _highContrast: highContrast } })

const highContrastPalettes = {
  gray: '#ffffff',
  red: '#ff8f8f',
  orange: '#ffb366',
  yellow: '#ffeb3b',
  green: '#6ef59a',
  blue: '#8fb8ff',
  purple: '#d9adff',
  teal: '#66f2e0',
}

const paletteOverrides = Object.fromEntries(
  Object.entries(highContrastPalettes).map(([palette, color]) => [
    palette,
    {
      solid: { value: { _highContrast: color } },
      contrast: { value: { _highContrast: '#000000' } },
      fg: { value: { _highContrast: color } },
      muted: { value: { _highContrast: '#1a1a1a' } },
      subtle: { value: { _highContrast: '#000000' } },
      emphasized: { value: { _highContrast: '#333333' } },
      focusRing: { value: { _highContrast: '#ffff00' } },
    },
  ]),
)

const fontStack = "'Open Sans Variable', 'Open Sans', 'Segoe UI', system-ui, sans-serif"

const config = defineConfig({
  conditions: {
    highContrast: '[data-contrast=high] &',
  },
  globalCss: {
    html: {
      colorPalette: 'brand',
      fontSize: 'var(--app-font-scale, 100%)',
      scrollPaddingTop: '5rem',
    },
    body: {
      bg: 'bg.canvas',
      color: 'fg',
      fontFamily: 'body',
      lineHeight: '1.5',
    },
    'a:focus-visible, button:focus-visible, [role=button]:focus-visible, [tabindex]:focus-visible': {
      outline: '3px solid',
      outlineColor: 'focus.ring',
      outlineOffset: '2px',
    },
    '*, *::before, *::after': {
      _motionReduce: {
        animationDuration: '0.01ms !important',
        animationIterationCount: '1 !important',
        transitionDuration: '0.01ms !important',
        scrollBehavior: 'auto !important',
      },
    },
  },
  theme: {
    tokens: {
      fonts: {
        heading: { value: fontStack },
        body: { value: fontStack },
      },
      colors: {
        brand: {
          50: { value: '#edf3fb' },
          100: { value: '#d3e2f4' },
          200: { value: '#a7c4e9' },
          300: { value: '#729fd9' },
          400: { value: '#3a77c3' },
          500: { value: '#0052a5' },
          600: { value: '#00468d' },
          700: { value: '#003a75' },
          800: { value: '#002d5c' },
          900: { value: '#001f40' },
          950: { value: '#00142b' },
        },
        govRed: { value: '#d4213d' },
      },
    },
    semanticTokens: {
      colors: {
        ...paletteOverrides,
        brand: {
          solid: hc('{colors.brand.500}', '#ffff00'),
          contrast: hc('#ffffff', '#000000'),
          fg: hc('{colors.brand.600}', '#ffff00'),
          muted: hc('{colors.brand.100}', '#333333'),
          subtle: hc('{colors.brand.50}', '#1a1a1a'),
          emphasized: hc('{colors.brand.200}', '#4d4d4d'),
          focusRing: hc('{colors.brand.500}', '#ffff00'),
        },
        bg: {
          DEFAULT: hc('#ffffff', '#000000'),
          canvas: hc('#f3f4f6', '#000000'),
          subtle: hc('#f7f7f8', '#0d0d0d'),
          muted: hc('#f1f1f1', '#1a1a1a'),
          emphasized: hc('#e2e3e5', '#333333'),
          panel: hc('#ffffff', '#000000'),
        },
        fg: {
          DEFAULT: hc('#1b1b1b', '#ffffff'),
          muted: hc('#4f4f4f', '#f0f0f0'),
          subtle: hc('#616161', '#d9d9d9'),
          error: hc('#b91c1c', highContrastPalettes.red),
        },
        border: {
          DEFAULT: hc('#d9dadc', '#ffffff'),
          muted: hc('#e6e7e9', '#bfbfbf'),
          subtle: hc('#eeeff0', '#8c8c8c'),
          emphasized: hc('#767676', '#ffffff'),
        },
        focus: {
          ring: hc('{colors.brand.500}', '#ffff00'),
        },
        accent: {
          red: hc('{colors.govRed}', '#ffff00'),
        },
        row: {
          urgent: hc('#fdf1f2', '#000000'),
          warning: hc('#fff6eb', '#000000'),
        },
      },
    },
    recipes: {
      button: {
        base: { fontWeight: 'semibold' },
        variants: {
          variant: {
            outline: { borderWidth: '2px', borderColor: 'colorPalette.solid', color: 'colorPalette.fg' },
          },
        },
      },
      input: {
        variants: { variant: { outline: { borderColor: 'border.emphasized' } } },
      },
      textarea: {
        variants: { variant: { outline: { borderColor: 'border.emphasized' } } },
      },
      heading: {
        base: { fontWeight: 'bold', color: 'fg' },
      },
    },
    slotRecipes: {
      nativeSelect: {
        slots: nativeSelectAnatomy.keys(),
        variants: { variant: { outline: { field: { borderColor: 'border.emphasized' } } } },
      },
      datePicker: {
        slots: datePickerAnatomy.keys(),
        variants: { variant: { outline: { input: { borderColor: 'border.emphasized' } } } },
      },
      segmentGroup: {
        slots: segmentGroupAnatomy.keys(),
        base: {
          root: {
            '--segment-radius': 'radii.l2',
            bg: 'bg.panel',
            boxShadow: 'inset 0 0 0 1px var(--chakra-colors-border-emphasized)',
          },
          item: {
            fontWeight: 'semibold',
            color: 'fg',
            _before: { display: 'none' },
            _checked: { color: 'brand.contrast' },
            '&:not([data-state=checked]):hover': { bg: 'brand.subtle', color: 'brand.fg' },
            '&[data-state=checked][data-ssr]': { shadow: 'none', bg: 'brand.solid' },
          },
          indicator: {
            bg: 'brand.solid',
            shadow: 'none',
          },
        },
      },
      checkbox: {
        slots: checkboxAnatomy.keys(),
        variants: { variant: { outline: { control: { borderColor: 'border.emphasized' } } } },
      },
    },
  },
})

export const system = createSystem(defaultConfig, config)
