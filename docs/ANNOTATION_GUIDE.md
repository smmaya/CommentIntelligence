# Furniture comment annotation guide

This guide defines the annotation contract for the current Core pipeline. It is
separate from the Demo CSVs: the Demo data is synthetic and useful for regression
testing, but it is not evidence of production accuracy.

## Sentiment stars

Assign the overall product sentiment expressed by the comment:

- **1**: strongly negative; unusable, dangerous, fraudulent, or an explicit recommendation not to buy.
- **2**: mostly negative, with limited redeeming value.
- **3**: mixed, neutral, average, or explicitly “fine/acceptable”.
- **4**: mostly positive, with minor reservations.
- **5**: strongly positive and clearly recommended.

Use the overall product experience, not one isolated adjective. Delivery or seller
complaints count only when they are part of the customer's overall purchase
experience. If sentiment is genuinely ambiguous, use **3** and flag the example
for adjudication.

These are inferred sentiment labels, not verified customer star ratings. Explicit
abuse or configured strong-negative language is forced to 1 star by the safety
policy, regardless of the sentiment model's ordinary prediction.

## Content labels

Content labels are independently scored. A comment may receive a primary label
plus up to two secondary labels:

1. **Hateful** — attacks people or protected groups, or uses explicit abuse.
2. **Tendentious** — promotional, absolutist, manipulative, or competitor-directed
   language that makes unsupported claims.
3. **Helpful** — gives actionable buying, use, assembly, sizing, care, or
   comparison advice.
4. **Informative** — gives concrete product facts without actionable advice.
5. **Emotional** — primarily expresses a feeling or reaction with little factual
   content.
6. **LowQuality** — too vague, empty, repetitive, or contentless to help a buyer.

When multiple labels apply, select the label that best represents the comment's
dominant purpose as the primary label, then record the other applicable labels as
secondary labels. Do not add a secondary label merely because a single word
appears; it must describe a meaningful part of the comment.

The runtime uses independent one-vs-rest models. It returns labels with sufficient
independent evidence, keeps at most three labels total, and may return only one.
The primary label remains authoritative for visibility scoring. Secondary labels
are explanatory and do not independently change visibility.

## Annotation process

- Two annotators independently label every gold-set example.
- Record a short rationale for every `Hateful`, `Tendentious`, or `LowQuality` label.
- Record the primary label first and list any meaningful secondary labels.
- Adjudicate disagreements without silently overwriting the original labels.
- Split train and validation data by review/template/source group, never by random row.
- Keep product, seller, delivery, and assembly topics represented in every language.
- Keep equivalent examples aligned across languages; do not translate an informative
  comment into an emotional or promotional one.
