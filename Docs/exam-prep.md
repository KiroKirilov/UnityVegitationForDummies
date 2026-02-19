# AI for Developers – January 2026

## Individual Project Assignment

## Project Description

cAIdence is a personal music diary web application that connects to your Spotify account and helps you explore and reflect on your listening habits over time. Instead of only showing raw statistics, it presents your listening history as a day-by-day story you can revisit.

After signing in with Spotify, you can view your profile (name, email, avatar, plan, and follower count) and open the Recent Activity page to review your most recently played tracks in chronological order, grouped by day.

Each track entry includes useful context such as artist/album, duration, popularity, and whether the track is saved in your library. The activity view also includes interactive charts that summarize listening patterns, letting you compare artists by track count or total listening time and see daily listening trends.

**Integration:** The app uses the Spotify Web API to fetch profile and listening history for the currently signed-in user. The experience is read-only and focused on personal insight and reflection.

## System Architecture – Modules

The system is organized into four technological modules that map to the end-to-end user experience (sign in → fetch data → present data → summarize/visualize):

1. **Authentication** – Approach: authenticate users with Spotify and maintain a secure session so the app can access user-specific data without repeatedly asking the user to log in.
2. **Backend (Spotify API integration)** – Approach: provide server-side endpoints that call the Spotify Web API to retrieve the signed-in user's profile and recent listening history, and return clean, UI-ready data to the client.
3. **Frontend (UI & navigation)** – Approach: deliver a simple, visually appealing interface with clear navigation (home, profile, recent activity), helpful loading/error states, and readable presentation of tracks grouped by day.
4. **Statistics (charts & future analysis)** – Approach: transform listening history into summaries that help users spot patterns quickly (e.g., daily trends and artist-based breakdowns), and provide a foundation for richer insights over time.

**AI assistance (all modules):** A "Beatify prompt" was used to generate a plan-like outline that was later reviewed, corrected, and executed (via Augment Code) across the entire project.

## Development Process per Module

**Overview:** I started with a minimal Next.js project setup and worked incrementally. Authentication was the first major milestone and took most of the time to implement and refine. To validate that sign-in and session handling worked end-to-end, I introduced a simple Profile page that renders standard information about the currently authenticated user. After that foundation was stable, the rest of the work followed an incremental sequence: recent listening activity first, then statistics/charts, and finally richer per-track information (duration, album artwork, artist, popularity index, saved/liked status). The commit history captures the step-by-step plan and evolution. Throughout development, I used a feedback loop with subagents that forced iteration until a condition was satisfied.

### Module 1 – Authentication

**Approach & reasoning:** prioritize a reliable login + session foundation so every other module can assume an authenticated user context.

**Step-by-step workflow:** scaffold the auth provider, wire the sign-in/sign-out UX in the header, add a post-login redirect, and introduce the Profile page as the first proof that the session and Spotify identity are working.

**Testing strategy:** manual end-to-end checks (sign in, refresh, navigate, sign out) plus the subagent feedback loop to catch and fix build/type errors immediately as changes were introduced.

**AI tool choice:** Augment Code for rapid iteration directly in the repo; outputs were reviewed and then refined through repeated small changes.

**Key prompts:** see `setup_authentication_prompt` below.

### Module 2 – Backend (Spotify API calling)

**Approach & reasoning:** centralize Spotify Web API calls behind server-side routes so the UI only consumes app-owned endpoints and receives consistent, UI-ready responses.

**Step-by-step workflow:** add endpoints for profile and recently played tracks, handle common failure modes (not authenticated, token expiry, upstream errors), then enrich responses with additional context useful to the UI (for example, saved/liked status).

**Testing strategy:** validate with real login sessions and repeated refresh/navigation, confirm correct behavior for unauthenticated requests, and use the subagent loop to keep the codebase compiling as routes evolved.

**AI tool choice:** Augment Code to generate route scaffolding and data-shaping steps quickly, then iteratively refine based on runtime behavior.

**Key prompts:** see `load_listening_activity` below.

### Module 3 – Frontend (UI)

**Approach & reasoning:** keep the UI simple and responsive, with clear navigation (home → profile → recent activity) and strong feedback (loading, empty, error states) so users always understand what is happening.

**Step-by-step workflow:** build the Profile page first (as an authentication validation surface), then the Recent Activity page with a day-grouped chronological view, and finally enhance track rows with richer details that improve readability and context.

**Testing strategy:** manual UX checks across navigation paths and screen sizes, plus continuous compilation/type checks as UI components were added and adjusted.

**AI tool choice:** Augment Code to draft UI structures rapidly, then adjust based on real data and edge cases found during navigation/testing.

### Module 4 – Statistics (charts + future analysis)

**Approach & reasoning:** convert raw listening history into summaries that are immediately useful (patterns by artist and by day), while keeping metrics explainable and easy to compare via toggles.

**Step-by-step workflow:** start with a working activity list, then layer in aggregation for charts, then iterate on usability (metric toggle, labeling) and expand per-track context that supports interpretation.

**Testing strategy:** sanity-check aggregates against the visible track list, verify the metric toggle updates consistently, and use the subagent loop to ensure each iteration builds cleanly.

**AI tool choice:** Augment Code for fast prototyping of aggregation + chart wiring, followed by review and incremental corrections.

**Key prompts:** see `add_artist_statistics` below.

---

### Key Prompts

#### `setup_authentication_prompt`

```xml
<goal>
Implement Spotify OAuth 2.0 authentication with PKCE (Proof Key for Code Exchange)
flow to allow users to sign in with their Spotify accounts and view their profile
information.
</goal>
<tasks>
- Add a new "Sign in" button to the application header. Ensure the button is visible
  only when the user is not yet authenticated.
- Implement Spotify OAuth 2.0 Flow.
    - The PKCE Flow must be implemented.
    - Do not allow automatic login without showing a consent screen (in case there is
      an already valid Spotify session cookie in the browser). The flow should give the
      user a chance to choose which Spotify account to use.
    - All necessary application parameters will be set in `.env*` files.
    - Make sure redirect callbacks are handled correctly.
- Use built-in mechanisms (like Next Auth) and do not re-invent the wheel.
- Obtain an access token. Refresh if/when necessary.
- Do NOT store the access token in local storage. Configure server-side sessions with
  HTTP only cookies instead.
- Get the current user's profile details and display them in a separate page where
  the user is redirected immediately after login.
</tasks>
<additional-resources>
Spotify API reference:
- Authorization Guide: https://developer.spotify.com/documentation/web-api/concepts/authorization
- PKCE Flow Specifics: https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow
- Get current user's profile: https://developer.spotify.com/documentation/web-api/reference/get-current-users-profile
</additional-resources>
```

#### `load_listening_activity`

```xml
<goal>
Add a new page that displays the listening history of the current user.
</goal>
<tasks>
- Add a new page dedicated to visualizing the listening history of the current user
    - Add an appropriate message displaying information that Spotify gives a very
      limited access to the listening history of a user (only the last X entries).
    - Songs should be displayed in groups by days and in chronological order
      (oldest first)
- Use the `Get Recently Played Tracks` Spotify API to fetch all necessary information
    - This endpoint has some known limitations related to the maximum number of tracks
      returned.
- The information should be loaded on demand and not persisted anywhere.
</tasks>
<additional-resources>
- Get Recently Played Tracks: https://developer.spotify.com/documentation/web-api/reference/get-recently-played
</additional-resources>
```

#### `add_artist_statistics`

```xml
<goal>
Add interactive statistics visualizations to the listening activity page to display
music consumption patterns grouped by artist.
The user should be able to toggle between two metrics: track count (number of tracks
played) and listening time (total duration).
</goal>
<tasks>
- Implement two complementary charts for artist statistics:
    - Pie chart: Display aggregate totals across all artists (e.g., total tracks or
      total listening time per artist)
    - Area chart: Display time-series data showing per-day breakdown of the selected
      metric by artist
- Add a metric toggle control with the following requirements:
    - Provide two options: "Track Count" and "Listening Time"
    - Add a question mark (?) icon next to the toggle that displays a tooltip on hover
      explaining:
        - What "Track Count" means (number of individual tracks played)
        - What "Listening Time" means (total duration of playback)
- Integrate both charts into the listening activity page layout:
    - Position the pie chart and area chart horizontally aligned (side-by-side on
      larger screens)
    - Ensure the layout is responsive and stacks vertically on smaller screens
    - Place the metric toggle above the charts
- Apply appropriate styling:
    - Use consistent colors across both charts for the same artists
    - Ensure charts are visually balanced and properly sized
    - Follow the existing design system and color palette of the application
    - Make interactive elements (toggle, tooltip) clearly clickable/hoverable
- Handle edge cases and test with various data scenarios:
    - Empty data: Display a meaningful empty state message
    - Single artist: Ensure charts render correctly with minimal data
    - Large datasets: Test performance and readability with many artists (10+)
    - Date ranges: Verify the area chart handles different time periods correctly
- Verify that:
    - Charts update immediately when toggling between metrics
    - Data is accurately represented in both visualization types
    - All interactive elements are accessible and functional
</tasks>
```

---

## Challenges & Tool Comparison

I used Augment Code exclusively, but... throughout the course I experimented with Copilot, Cursor and Claude Code, too. I found many similarities but the best one for me is...

## Working System Evidence

## Repository

https://github.com/TonyTroeff/cAIdence
