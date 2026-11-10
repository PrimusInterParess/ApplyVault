# ADR-0041: A Work in Denmark Place is one town within 50 km

## Status

Accepted (2026-11-10 — grilling session on the Work in Denmark search query)

## Context

A Work in Denmark search query is a keyword search. Seekers also want to narrow it to a place. The Work in Denmark search API does not accept a city name. A `city` parameter is ignored. Putting the town into the keyword string is a text match, not a place. `kmRadius` without a center does not filter. The API narrows by place only with one `postalCode` plus `kmRadius`, or with one region, and those two modes are not combined. A Place with no keyword returns every kind of job near that town.

## Decision

1. **A Place is optional and a query has at most one.** The seeker chooses Copenhagen, Aarhus, Odense, Aalborg, or Randers. With none chosen, the search covers all of Denmark.
2. **Each town has one center postal code.** Copenhagen is `1050`, Aarhus is `8000`, Odense is `5000`, Aalborg is `9000`, and Randers is `8900`. At 50 km, Copenhagen centers `1050`, `1550`, and `2100` returned the same developer result count, so `1050` is the center. Randers centers `8900`, `8920`, and `8960` likewise returned the same count, so `8900` is the center.
3. **The Commute distance is 50 km for every Place.** The seeker does not set it. A chosen Place is sent as that postal code plus `kmRadius=50`. No Place sends neither.
4. **At least one keyword is required.** A Place narrows that keyword search and cannot stand alone.

## Consequences

- Two towns the seeker would accept are two queries.
- A region search, a typed city, a seeker-chosen radius, and a Place-only search stay out of the query.
- The ranked-search cache key must include the Place. Page size stays out of that key.
- Rejected: the five Work in Denmark regions, because a region is too coarse for a commute, and a free-text city, because the API does not treat it as a place.
