'use strict';

require('dotenv').config();

const express = require('express');
const Stripe = require('stripe');
const path = require('path');
const { fulfillCheckoutSession } = require('./fulfillment');

const {
  STRIPE_SECRET_KEY,
  STRIPE_PRICE_ID,
  DOMAIN = 'http://localhost:4242',
  STRIPE_WEBHOOK_SECRET,
  PORT = '4242',
} = process.env;

if (!STRIPE_SECRET_KEY) {
  console.error('Missing STRIPE_SECRET_KEY. Copy .env.example to .env and add your test keys.');
  process.exit(1);
}

// Prefer StripeClient-style instance usage (never set a global api_key).
const stripe = new Stripe(STRIPE_SECRET_KEY, {
  apiVersion: '2026-09-30.endive',
  appInfo: {
    name: 'VetTV Studios Checkout',
    version: '1.0.0',
    url: 'https://github.com/VetTV-Studios',
  },
});

const app = express();

// Webhooks need the raw body for signature verification — mount before json parser.
app.post(
  '/webhook',
  express.raw({ type: 'application/json' }),
  async (req, res) => {
    const signature = req.headers['stripe-signature'];

    let event;
    try {
      if (!STRIPE_WEBHOOK_SECRET) {
        console.warn(
          'STRIPE_WEBHOOK_SECRET is not set — skipping signature verification (dev only).'
        );
        event = JSON.parse(req.body.toString('utf8'));
      } else {
        event = stripe.webhooks.constructEvent(
          req.body,
          signature,
          STRIPE_WEBHOOK_SECRET
        );
      }
    } catch (err) {
      console.error('Webhook signature verification failed:', err.message);
      return res.status(400).send(`Webhook Error: ${err.message}`);
    }

    try {
      switch (event.type) {
        case 'checkout.session.completed':
        case 'checkout.session.async_payment_succeeded': {
          const session = event.data.object;
          await fulfillCheckoutSession(stripe, session);
          break;
        }
        case 'checkout.session.async_payment_failed': {
          const session = event.data.object;
          console.warn(
            `Async payment failed for session ${session.id}. Do not fulfill.`
          );
          break;
        }
        default:
          break;
      }
      res.json({ received: true });
    } catch (err) {
      console.error('Webhook handler error:', err);
      res.status(500).json({ error: 'Webhook handler failed' });
    }
  }
);

app.use(express.static(path.join(__dirname, 'public')));
app.use(express.urlencoded({ extended: false }));
app.use(express.json());

app.get('/config', (_req, res) => {
  res.json({
    publishableKey: process.env.STRIPE_PUBLISHABLE_KEY || null,
    hasPrice: Boolean(STRIPE_PRICE_ID),
  });
});

app.post('/create-checkout-session', async (req, res) => {
  if (!STRIPE_PRICE_ID) {
    return res.status(500).json({
      error:
        'STRIPE_PRICE_ID is not set. Run `npm run create-product` or set a Price ID in .env.',
    });
  }

  try {
    const integrationSuffix = Math.random().toString(36).slice(2, 10);
    const session = await stripe.checkout.sessions.create({
      mode: 'payment',
      line_items: [
        {
          price: STRIPE_PRICE_ID,
          quantity: 1,
        },
      ],
      // Omit payment_method_types — dynamic payment methods maximize conversion.
      success_url: `${DOMAIN}/success.html?session_id={CHECKOUT_SESSION_ID}`,
      cancel_url: `${DOMAIN}/cancel.html`,
      customer_creation: 'always',
      integration_identifier: `vettv_hosted_${integrationSuffix}`,
      metadata: {
        studio: 'VetTV Studios',
        fulfillment: 'digital_download',
      },
    });

    // Hosted Checkout: redirect the browser to Stripe's page.
    if (req.headers.accept && req.headers.accept.includes('application/json')) {
      return res.json({ url: session.url, id: session.id });
    }
    return res.redirect(303, session.url);
  } catch (err) {
    console.error('Failed to create Checkout Session:', err);
    return res.status(500).json({ error: err.message });
  }
});

app.get('/session-status', async (req, res) => {
  const sessionId = req.query.session_id;
  if (!sessionId || typeof sessionId !== 'string') {
    return res.status(400).json({ error: 'session_id is required' });
  }

  try {
    const session = await stripe.checkout.sessions.retrieve(sessionId);
    return res.json({
      status: session.status,
      payment_status: session.payment_status,
      customer_email: session.customer_details?.email || null,
    });
  } catch (err) {
    console.error('Failed to retrieve session:', err);
    return res.status(500).json({ error: err.message });
  }
});

app.listen(Number(PORT), () => {
  console.log(`VetTV Studios checkout listening on ${DOMAIN} (port ${PORT})`);
});
