# VetTV Studios — Stripe Checkout

Stripe-hosted **Checkout** starter for one-time digital product payments (games/tools storefront).

This folder is intentionally separate from the Xbox Game Bar Direct Stream Widget. Microsoft Store / Game Bar apps should not sell digital goods through Stripe in-app; use a web storefront instead.

## What you get

1. Create a Product + Price
2. Redirect buyers to Stripe-hosted Checkout
3. Fulfill orders from webhooks (`checkout.session.completed` + `checkout.session.async_payment_succeeded`)

## Setup

```bash
cd studio-checkout
cp .env.example .env
# Put test/sandbox keys in .env (prefer rk_ / rkcs_ restricted keys)
npm install
npm run create-product
npm run dev
```

In a second terminal, forward webhooks:

```bash
npx stripe listen --forward-to localhost:4242/webhook
```

Paste the printed `whsec_…` into `.env` as `STRIPE_WEBHOOK_SECRET`, then restart the server.

Open http://localhost:4242 and pay with test card `4242 4242 4242 4242`.

## Plan decisions (from Stripe implementation planner)

| Decision | Choice |
| --- | --- |
| Processor | Stripe only |
| Surface | Web browser |
| Checkout | Stripe-hosted Checkout (redirect) |
| Mode | `payment` (one-time) |
| Payment methods | Dynamic (do not hardcode `payment_method_types`) |

Docs: [Stripe-hosted Checkout quickstart](https://docs.stripe.com/checkout/quickstart)

## Go-live checklist

- [ ] Claim/connect your real Stripe account (not the temporary sandbox)
- [ ] Use a [restricted API key](https://docs.stripe.com/keys#manage-your-api-keys)
- [ ] Replace the placeholder product with real catalog prices
- [ ] Implement real fulfillment in `fulfillment.js` (license email / download unlock)
- [ ] Deploy with HTTPS and a Dashboard webhook endpoint
- [ ] Before enabling `automatic_tax`, add an active [tax registration](https://docs.stripe.com/tax)
- [ ] Complete the [go-live checklist](https://docs.stripe.com/get-started/checklist/go-live)
