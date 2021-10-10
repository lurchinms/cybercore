#!/bin/bash

#############################################
# Created By CyperPool for CyberCore use... #
#############################################

source /etc/functions.sh
source /etc/web.conf

echo
echo
echo -e "$CYAN=> Generating Certbot Request For ${Domain_Name}...$COL_RESET"
echo
sleep 3

sudo mkdir -p /var/www/${Domain_Name}/html
sudo rm -rf /etc/nginx/sites-available/${Domain_Name}
sudo rm -rf /etc/nginx/sites-enabled/${Domain_Name}

echo '#############################################
# Source Generated By nginxconfig.io        #
# Updated By CyperPool for CyberCore use... #
#############################################

# NGINX Simple DDOS Defense
limit_conn_zone $binary_remote_addr zone=conn_limit_per_ip:10m;
limit_conn conn_limit_per_ip 80;
limit_req zone=req_limit_per_ip burst=80 nodelay;
limit_req_zone $binary_remote_addr zone=req_limit_per_ip:40m rate=5r/s;

server {
	root "/var/www/'"${Domain_Name}"'/html";

	index index.html index.htm index.php;

	server_name '"${Domain_Name}"';

	location / {
		try_files $uri $uri/ =404;
	}

	listen [::]:443 ssl; # managed by Certbot
	listen 443 ssl; # managed by Certbot
	ssl_certificate /etc/letsencrypt/live/'"${Domain_Name}"'/fullchain.pem; # managed by Certbot
	ssl_certificate_key /etc/letsencrypt/live/'"${Domain_Name}"'/privkey.pem; # managed by Certbot
	include /etc/letsencrypt/options-ssl-nginx.conf; # managed by Certbot
	ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem; # managed by Certbot
}

server {
	if ($host = '"${Domain_Name}"') {
		return 301 https://$host$request_uri;
	} # managed by Certbot

	listen 80;
	listen [::]:80;

	server_name '"${Domain_Name}"';
	return 404; # managed by Certbot
}
' | sudo -E tee /etc/nginx/sites-available/${Domain_Name} >/dev/null 2>&1

sudo ln -s /etc/nginx/sites-available/${Domain_Name} /etc/nginx/sites-enabled/${Domain_Name}

sudo systemctl restart nginx
cd $HOME/