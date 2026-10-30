'use strict';

require('dotenv').config({ path: require('path').join(__dirname, '..', '.env') });

const fs = require('fs');
const path = require('path');
const Stripe = require('stripe');

const secret = process.env.STRIPE_SECRET_KEY;
if (!secret) {
  console.error('Missing STRIPE_SECRET_KEY in .env');
  process.exit(1);
}

const stripe = new Stripe(secret, {
  apiVersion: '2026-09-30.endive',
});

async function main() {
  const name =
    process.argv[2] || 'VetTV Studios — Indie Digital Title (placeholder)';
  const unitAmount = Number(process.argv[3] || 1999); // $19.99
  const currency = (process.argv[4] || 'usd').toLowerCase();

  const product = await stripe.products.create({
    name,
    description:
      'Placeholder digital product for VetTV Studios Checkout. Replace with a real game or stream tool.',
    metadata: {
      studio: 'VetTV Studios',
      type: 'digital',
    },
    default_price_data: {
      unit_amount: unitAmount,
      currency,
    },
  });

  const priceId = product.default_price;
  console.log('Created product:', product.id);
  console.log('Default price:', priceId);

  const envPath = path.join(__dirname, '..', '.env');
  if (fs.existsSync(envPath)) {
    let env = fs.readFileSync(envPath, 'utf8');
    if (/^STRIPE_PRICE_ID=/m.test(env)) {
      env = env.replace(/^STRIPE_PRICE_ID=.*$/m, `STRIPE_PRICE_ID=${priceId}`);
    } else {
      env += `\nSTRIPE_PRICE_ID=${priceId}\n`;
    }
    fs.writeFileSync(envPath, env);
    console.log('Updated STRIPE_PRICE_ID in .env');
  } else {
    console.log('Add this to your .env:');
    console.log(`STRIPE_PRICE_ID=${priceId}`);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
