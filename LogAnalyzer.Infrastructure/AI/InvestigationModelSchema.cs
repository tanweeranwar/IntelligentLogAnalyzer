namespace LogAnalyzer.Infrastructure.AI;

internal static class InvestigationModelSchema
{
    public const string JsonSchema =
        """
        {
          "type": "object",
          "properties": {
            "executiveSummary": {
              "type": "string"
            },
            "nextAction": {
              "type": "object",
              "properties": {
                "title": { "type": "string" },
                "action": { "type": "string" },
                "reason": { "type": "string" },
                "expectedOutcome": { "type": "string" },
                "confidenceScore": {
                  "type": "integer",
                  "minimum": 0,
                  "maximum": 100
                }
              },
              "required": [
                "title",
                "action",
                "reason",
                "expectedOutcome",
                "confidenceScore"
              ]
            },
            "rootCauses": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "title": { "type": "string" },
                  "explanation": { "type": "string" },
                  "confidenceScore": {
                    "type": "integer",
                    "minimum": 0,
                    "maximum": 100
                  },
                  "supportingEvidence": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "contradictingEvidence": {
                    "type": "array",
                    "items": { "type": "string" }
                  }
                },
                "required": [
                  "title",
                  "explanation",
                  "confidenceScore",
                  "supportingEvidence",
                  "contradictingEvidence"
                ]
              }
            },
            "investigationSteps": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "sequence": { "type": "integer" },
                  "title": { "type": "string" },
                  "action": { "type": "string" },
                  "reason": { "type": "string" },
                  "expectedOutcome": { "type": "string" },
                  "priority": { "type": "string" },
                  "confidenceScore": {
                    "type": "integer",
                    "minimum": 0,
                    "maximum": 100
                  }
                },
                "required": [
                  "sequence",
                  "title",
                  "action",
                  "reason",
                  "expectedOutcome",
                  "priority",
                  "confidenceScore"
                ]
              }
            },
            "resolutionRecommendations": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "title": { "type": "string" },
                  "description": { "type": "string" },
                  "recommendationType": { "type": "string" },
                  "risk": { "type": "string" },
                  "confidenceScore": {
                    "type": "integer",
                    "minimum": 0,
                    "maximum": 100
                  }
                },
                "required": [
                  "title",
                  "description",
                  "recommendationType",
                  "risk",
                  "confidenceScore"
                ]
              }
            },
            "overallConfidenceScore": {
              "type": "integer",
              "minimum": 0,
              "maximum": 100
            },
            "unknowns": {
              "type": "array",
              "items": { "type": "string" }
            }
          },
          "required": [
            "executiveSummary",
            "nextAction",
            "rootCauses",
            "investigationSteps",
            "resolutionRecommendations",
            "overallConfidenceScore",
            "unknowns"
          ]
        }
        """;
}