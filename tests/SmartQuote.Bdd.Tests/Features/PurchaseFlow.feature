Feature: Purchase request and quotation intake
  As SmartQuote users with assigned roles
  We need purchase requests and quotations to be traceable across the API and database

  Scenario: Production registers a complete purchase request
    Given the SmartQuote API and PostgreSQL are available
    And I have the ProductionSpecialist role
    When I register a complete purchase request
    Then the API responds with 201
    And the request can be read back with Submitted status

  Scenario: Purchasing cannot register a production request
    Given the SmartQuote API and PostgreSQL are available
    And I have the PurchaseAnalyst role
    When I register a complete purchase request
    Then the API responds with 403

  Scenario: A batch preserves a valid quotation when another file is unsupported
    Given the SmartQuote API and PostgreSQL are available
    And a request has reached QuotationCollection status
    When an analyst uploads one PDF and one unsupported file in a batch
    Then the API responds with 207
    And the PDF is persisted while the unsupported file is rejected
