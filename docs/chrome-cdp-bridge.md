# Chrome/CDP Bridge

The Chrome/CDP provider is the primary local interactive browser backend for DEVOS. It is intended to control an already-installed, user-approved Chrome session through a thin Chrome extension and either secure localhost transport or native messaging.

## Boundary

The Chrome extension is a bridge, not the DEVOS brain. DEVOS Core remains independent of Chrome and asks only for browser capabilities. The Chrome/CDP provider may deeply use Chrome extension APIs and CDP domains, but it must expose normalized `IBrowserAdapter` behavior.

## Handshake

The runtime and bridge must negotiate:

- extension id and extension version
- transport mode: localhost or native messaging
- runtime endpoint
- protocol version
- requested capabilities
- granted capabilities

Remote runtime endpoints are rejected by default for localhost transport. They require an explicit configuration flag because local browser control must not silently connect to arbitrary remote servers.

## Current implementation status

This slice adds the protocol skeleton only:

- bridge options
- handshake request/result models
- capability grant filtering
- loopback endpoint enforcement
- in-memory transport for tests
- non-executing `ChromeCdpBrowserAdapter`

The adapter intentionally fails closed for real browser actions until the actual CDP executor is implemented and tested.

## Next implementation slice

The next Chrome slice should add the extension package skeleton: Manifest V3, service worker, runtime handshake, localhost/native-messaging transport code, and explicit user-visible connection state.
