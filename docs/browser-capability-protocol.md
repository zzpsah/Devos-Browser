# DEVOS Browser Capability Protocol

Initial protocol version: `1.0`.

## Purpose

The protocol is the common language between DEVOS Core and browser/extraction providers. DEVOS Core plans against capabilities and normalized observations, never provider-specific objects.

## Capabilities

- `browser.navigate`
- `browser.back`
- `browser.forward`
- `browser.reload`
- `browser.tab.open`
- `browser.tab.close`
- `browser.tab.list`
- `browser.tab.switch`
- `browser.observe`
- `browser.dom.query`
- `browser.dom.read`
- `browser.dom.attribute`
- `browser.dom.visible`
- `browser.click`
- `browser.doubleClick`
- `browser.hover`
- `browser.scroll`
- `browser.type`
- `browser.keypress`
- `browser.select`
- `browser.wait.selector`
- `browser.wait.text`
- `browser.wait.navigation`
- `browser.wait.networkIdle`
- `browser.extract.text`
- `browser.extract.table`
- `browser.extract.links`
- `browser.download`
- `browser.upload`
- `browser.screenshot`
- `browser.network.observe`
- `browser.frame.list`
- `browser.frame.switch`
- `browser.accessibility.snapshot`
- `browser.form.inspect`
- `browser.form.fill`
- `browser.form.submit`

## Observation model

Adapters return a normalized `BrowserObservation` with URL, title, tab id, visible text, element refs, forms, tables, frames, and network state.

Temporary refs like `d12` are used instead of raw selectors as planner identity.
