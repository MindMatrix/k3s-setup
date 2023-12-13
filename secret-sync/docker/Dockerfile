FROM alpine:latest
RUN apk add --no-cache bash kubectl
COPY sync-secrets.sh /sync-secrets.sh
RUN chmod +x /sync-secrets.sh
CMD ["/sync-secrets.sh"]
