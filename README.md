# log4net.SlackAppender

Send log to Slack.

### log4net config

```xml
<log4net>
  <appender name="SlackAppender" type="log4net.Appender.SlackAppender, log4net.SlackAppender">
    <webhookUrl>{Your WebhookUrl}</webhookUrl>
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%type{1}.%method%newline%message" />
    </layout>
    <filter type="log4net.Filter.LevelRangeFilter">
      <levelMin value="WARN"/>
      <levelMax value="ERROR"/>
    </filter>
  </appender>
  <root>
    <level value="INFO"/>
    <appender-ref ref="SlackAppender"/>
  </root>
</log4net>
```