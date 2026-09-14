import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  imports: [RouterLink],
  template: `
    <main>
      <section class="hero">
        <div class="hero-copy">
          <p class="eyebrow">THE PEOPLE PLATFORM</p>
          <h1>Where great teams<br><em>find each other.</em></h1>
          <p class="lede">One focused place to discover meaningful work, meet exceptional candidates, and move hiring forward.</p>
          <div class="actions"><a class="button primary" routerLink="/jobs">Explore open roles</a><a class="button light" routerLink="/register">Start hiring</a></div>
          <div class="proof"><strong>Built for momentum</strong><span>Clear pipelines</span><span>Fair access</span><span>Fewer spreadsheets</span></div>
        </div>
        <div class="hero-card">
          <p class="muted">FEATURED OPPORTUNITY</p><span class="pill">Remote</span>
          <h2>Senior Product Engineer</h2><p>Build thoughtful tools used by modern teams around the world.</p>
          <div class="skill-row"><span>TypeScript</span><span>.NET</span><span>Product</span></div>
          <hr><div class="company-mark"><b>NL</b><div><strong>Northline Labs</strong><small>Technology · 51–200</small></div></div>
        </div>
      </section>
      <section class="value-grid"><article><b>01</b><h3>For candidates</h3><p>Discover clear, relevant roles and follow every application from one calm workspace.</p></article><article><b>02</b><h3>For hiring teams</h3><p>Publish roles, review people, and collaborate through a pipeline everyone understands.</p></article><article><b>03</b><h3>Designed for trust</h3><p>Ownership and company boundaries are enforced in the domain—not merely hidden in the UI.</p></article></section>
    </main>
  `,
})
export class Home {}
