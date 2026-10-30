'use strict';

/**
 * Fulfillment must run from webhooks — never from the success page alone.
 * Handle both checkout.session.completed and checkout.session.async_payment_succeeded,
 * and only fulfill when payment_status is not "unpaid".
 */

const fulfilledSessions = new Set();

async function fulfillCheckoutSession(stripe, session) {
  if (!session?.id) {
    throw new Error('Missing Checkout Session id');
  }

  // Idempotency: Stripe may deliver the same event more than once.
  if (fulfilledSessions.has(session.id)) {
    console.log(`Session ${session.id} already fulfilled — skipping.`);
    return { alreadyFulfilled: true };
  }

  // Re-fetch to trust server state over the webhook payload copy.
  const fresh = await stripe.checkout.sessions.retrieve(session.id, {
    expand: ['line_items'],
  });

  if (fresh.payment_status === 'unpaid') {
    console.log(
      `Session ${fresh.id} is unpaid (async method still pending) — not fulfilling yet.`
    );
    return { deferred: true };
  }

  // TODO: grant license / email download link / unlock game key for VetTV Studios products.
  console.log('Fulfilling digital order:', {
    sessionId: fresh.id,
    paymentStatus: fresh.payment_status,
    customerEmail: fresh.customer_details?.email || null,
    amountTotal: fresh.amount_total,
    currency: fresh.currency,
    lineItems: (fresh.line_items?.data || []).map((item) => ({
      description: item.description,
      quantity: item.quantity,
      amountTotal: item.amount_total,
    })),
  });

  fulfilledSessions.add(fresh.id);
  return { fulfilled: true };
}

module.exports = { fulfillCheckoutSession };
